namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Prompts: list, read, versions, and a topic override (create, save a version, rollback, reset, delete). Tenant prompts
    /// are only read.
    /// </summary>
    public class PromptsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Prompts list, get, versions, topic override lifecycle";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PromptsTest()
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
            EnumerationResult<PromptTemplate> page = await StepAsync(context, result, "GET prompts (tenant only)", () => c.Prompts.ListAsync(new PromptListOptions { TenantOnly = true, PageSize = 100 }, token)).ConfigureAwait(false);
            PromptTemplate? first = page.Objects.FirstOrDefault(p => p.TopicId == null);
            if (first == null) throw new SkipTestException("The tenant has no prompts.");
            PromptTemplateResponse prompt = await StepAsync(context, result, "GET prompt", () => c.Prompts.GetAsync(first.Id, token)).ConfigureAwait(false);
            Helpers.NotEmpty(prompt.Content, "prompt content");
            List<PromptTemplateVersion> versions = (await StepAsync(context, result, "GET prompt versions", () => c.Prompts.ListVersionsAsync(first.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.True(versions.Count >= 1, "at least one version");
            await StepAsync(context, result, "DELETE tenant prompt (409)", () => Helpers.ExpectErrorAsync(() => c.Prompts.DeleteAsync(first.Id, token), 409, "Conflict")).ConfigureAwait(false);

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "prompt", token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST override with an unknown key (400)", () => Helpers.ExpectErrorAsync(() => c.Prompts.CreateOverrideAsync(new PromptOverrideRequest { Key = "no.such.key", TopicId = topic.Id }, token), 400)).ConfigureAwait(false);
            PromptTemplateResponse ov = await StepAsync(context, result, "POST topic override", () => c.Prompts.CreateOverrideAsync(new PromptOverrideRequest { Key = first.Key, TopicId = topic.Id }, token)).ConfigureAwait(false);
            string ovId = ov.Template.Id;
            context.Cleanup.Add("prompt:" + ovId, ct => c.Prompts.DeleteAsync(ovId, ct));
            Helpers.Equal(topic.Id, ov.Template.TopicId, "override topic");
            await StepAsync(context, result, "POST duplicate override (409)", () => Helpers.ExpectErrorAsync(() => c.Prompts.CreateOverrideAsync(new PromptOverrideRequest { Key = first.Key, TopicId = topic.Id }, token), 409)).ConfigureAwait(false);

            int v1 = ov.Template.CurrentVersion;
            PromptTemplateResponse saved = await StepAsync(context, result, "PUT override (new version)", () => c.Prompts.UpdateAsync(ovId, new PromptTemplateSaveRequest
            {
                Content = ov.Content + "\n",
                ChangeNote = "SDK harness"
            }, token)).ConfigureAwait(false);
            Helpers.True(saved.Template.CurrentVersion > v1, "version grows");
            PromptTemplateResponse rolled = await StepAsync(context, result, "POST override rollback", () => c.Prompts.RollbackAsync(ovId, v1, token)).ConfigureAwait(false);
            Helpers.NotEmpty(rolled.Content, "rolled back content");
            await StepAsync(context, result, "POST override rollback to a missing version (404)", () => Helpers.ExpectErrorAsync(() => c.Prompts.RollbackAsync(ovId, 9999, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "POST override reset", () => c.Prompts.ResetAsync(ovId, token)).ConfigureAwait(false);
            EnumerationResult<PromptTemplate> overrides = await StepAsync(context, result, "GET prompts (topic filter)", () => c.Prompts.ListAsync(new PromptListOptions { TopicId = topic.Id }, token)).ConfigureAwait(false);
            Helpers.Contains(overrides.Objects, p => p.Id == ovId, "the override");

            await StepAsync(context, result, "DELETE override", () => c.Prompts.DeleteAsync(ovId, token)).ConfigureAwait(false);
            context.Cleanup.Remove("prompt:" + ovId);
            await StepAsync(context, result, "GET deleted override (404)", () => Helpers.ExpectErrorAsync(() => c.Prompts.GetAsync(ovId, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
