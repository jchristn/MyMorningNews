namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Learned preferences (CRUD, enable, disable, accept and reject conflicts) and feedback with a reason on a preference
    /// (submit, list, update, retract, and typed validation errors).
    /// </summary>
    public class PreferencesFeedbackTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Learned preferences and feedback with reason";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PreferencesFeedbackTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "prefs", token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST preference without a statement (400)",
                () => Helpers.ExpectErrorAsync(() => c.Preferences.CreateAsync(new PreferenceUpsertRequest { TopicId = topic.Id }, token), 400, "ValidationFailed")).ConfigureAwait(false);

            LearnedPreference pref = await StepAsync(context, result, "POST preference", () => c.Preferences.CreateAsync(new PreferenceUpsertRequest
            {
                Statement = context.Name("pref") + " prefer primary sources",
                Kind = PreferenceKindEnum.PreferSource,
                TopicId = topic.Id
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("preference:" + pref.Id, ct => c.Preferences.DeleteAsync(pref.Id, ct));
            Helpers.Equal(PreferenceStatusEnum.Active, pref.Status, "a written preference is active at once");

            LearnedPreference edited = await StepAsync(context, result, "PUT preference", () => c.Preferences.UpdateAsync(pref.Id, new PreferenceUpsertRequest { Kind = PreferenceKindEnum.Prefer }, token)).ConfigureAwait(false);
            Helpers.Equal(PreferenceKindEnum.Prefer, edited.Kind, "kind");
            LearnedPreference disabled = await StepAsync(context, result, "POST disable", () => c.Preferences.DisableAsync(pref.Id, token)).ConfigureAwait(false);
            Helpers.Equal(PreferenceStatusEnum.Disabled, disabled.Status, "disabled");
            LearnedPreference enabled = await StepAsync(context, result, "POST enable", () => c.Preferences.EnableAsync(pref.Id, token)).ConfigureAwait(false);
            Helpers.Equal(PreferenceStatusEnum.Active, enabled.Status, "enabled");
            await StepAsync(context, result, "POST accept a non-proposed preference (409)", () => Helpers.ExpectErrorAsync(() => c.Preferences.AcceptAsync(pref.Id, token), 409, "Conflict")).ConfigureAwait(false);
            await StepAsync(context, result, "POST reject a non-proposed preference (409)", () => Helpers.ExpectErrorAsync(() => c.Preferences.RejectAsync(pref.Id, token), 409, "Conflict")).ConfigureAwait(false);
            EnumerationResult<LearnedPreference> prefs = await StepAsync(context, result, "GET preferences (topic filter)",
                () => c.Preferences.ListAsync(new PreferenceListOptions { TopicId = topic.Id, Origin = PreferenceOriginEnum.User }, token)).ConfigureAwait(false);
            Helpers.Contains(prefs.Items, p => p.Id == pref.Id, "the preference");

            await StepAsync(context, result, "POST feedback with an action that does not fit (400)", () => Helpers.ExpectErrorAsync(() => c.Feedback.SubmitAsync(new FeedbackCreateRequest
            {
                TargetType = FeedbackTargetTypeEnum.LearnedPreference,
                TargetId = pref.Id,
                Action = "useful"
            }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            await StepAsync(context, result, "POST feedback with nothing rated (400)", () => Helpers.ExpectErrorAsync(() => c.Feedback.SubmitAsync(new FeedbackCreateRequest
            {
                TargetType = FeedbackTargetTypeEnum.LearnedPreference,
                TargetId = pref.Id
            }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            await StepAsync(context, result, "POST feedback on a missing finding (404)", () => Helpers.ExpectErrorAsync(() => c.Feedback.SubmitAsync(new FeedbackCreateRequest
            {
                TargetType = FeedbackTargetTypeEnum.Finding,
                TargetId = "fnd_missing",
                Action = "relevant"
            }, token), 404)).ConfigureAwait(false);

            Feedback fb = await StepAsync(context, result, "POST feedback with reason", () => c.Feedback.SubmitAsync(new FeedbackCreateRequest
            {
                TargetType = FeedbackTargetTypeEnum.LearnedPreference,
                TargetId = pref.Id,
                Relevance = RelevanceRatingEnum.Relevant,
                Reason = "SDK harness: this matches how I read.",
                Channel = FeedbackChannelEnum.Api
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("feedback:" + fb.Id, ct => c.Feedback.DeleteAsync(fb.Id, ct));
            Helpers.Equal(topic.Id, fb.TopicId, "feedback is attributed to the preference's topic");
            Helpers.True(fb.IsCurrent, "feedback is current");

            EnumerationResult<Feedback> list = await StepAsync(context, result, "GET feedback (target filter)", () => c.Feedback.ListAsync(new FeedbackListOptions
            {
                TargetType = FeedbackTargetTypeEnum.LearnedPreference,
                TargetId = pref.Id
            }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Items, f => f.Id == fb.Id, "the feedback");
            Feedback updated = await StepAsync(context, result, "PUT feedback reason", () => c.Feedback.UpdateAsync(fb.Id, new FeedbackUpdateRequest { Reason = "Edited reason." }, token)).ConfigureAwait(false);
            Helpers.Equal("Edited reason.", updated.Reason, "updated reason");

            PreferenceDetailResponse detail = await StepAsync(context, result, "GET preference", () => c.Preferences.GetAsync(pref.Id, token)).ConfigureAwait(false);
            Helpers.Equal(pref.Id, detail.Preference.Id, "preference detail");

            await StepAsync(context, result, "DELETE feedback", () => c.Feedback.DeleteAsync(fb.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("feedback:" + fb.Id);
            await StepAsync(context, result, "PUT deleted feedback (404)", () => Helpers.ExpectErrorAsync(() => c.Feedback.UpdateAsync(fb.Id, new FeedbackUpdateRequest { Reason = "x" }, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE preference", () => c.Preferences.DeleteAsync(pref.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("preference:" + pref.Id);
            await StepAsync(context, result, "GET deleted preference (404)", () => Helpers.ExpectErrorAsync(() => c.Preferences.GetAsync(pref.Id, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
