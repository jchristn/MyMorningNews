namespace MyMorningNews.Core.Feedback
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Records reader feedback from any channel. The newest rating per user and item is current and earlier ones are kept.
    /// Some ratings also act: grading a prediction resolves it (with a Brier score), and Done or Snooze changes a
    /// suggestion. Thread safety: safe for concurrent use.
    /// </summary>
    public class FeedbackService
    {
        /// <summary>
        /// Longest reason kept.
        /// </summary>
        public const int MaxReasonLength = 2000;

        private readonly DatabaseDriverBase _Database;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public FeedbackService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Record a rating.
        /// </summary>
        /// <param name="input">Rating.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved feedback.</returns>
        /// <exception cref="ArgumentException">The action does not fit the item, or nothing was rated.</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The item does not exist or belongs to someone else.</exception>
        public async Task<Models.Feedback> RecordAsync(FeedbackInput input, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ApplyAction(input);
            string? reason = String.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
            if (reason != null && reason.Length > MaxReasonLength) reason = reason.Substring(0, MaxReasonLength);
            bool snoozeOrDone = input.Action == "snooze" || input.Action == "done" || input.Action == "apply" || input.Action == "dismiss";
            if (!input.Relevance.HasValue && !input.Accuracy.HasValue && !input.Usefulness.HasValue && reason == null && !snoozeOrDone)
                throw new ArgumentException("Choose a rating or write a reason.", nameof(input));

            string? topicId = await TargetTopicAsync(input, token).ConfigureAwait(false);
            string? previous = await _Database.Feedback.SupersedeAsync(input.TenantId, input.UserId, input.TargetType.ToString(), input.TargetId, token).ConfigureAwait(false);
            Models.Feedback saved = await _Database.Feedback.CreateAsync(new Models.Feedback
            {
                TenantId = input.TenantId,
                UserId = input.UserId,
                TargetType = input.TargetType,
                TargetId = input.TargetId,
                TopicId = topicId,
                Relevance = input.Relevance,
                Accuracy = input.Accuracy,
                Usefulness = input.Usefulness,
                Reason = reason,
                Channel = input.Channel,
                EmailMessageId = input.EmailMessageId,
                SupersedesFeedbackId = previous,
                IsCurrent = true
            }, token).ConfigureAwait(false);
            await SideEffectsAsync(input, token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.FeedbackSubmissions, "targettype", input.TargetType.ToString(), "channel", input.Channel.ToString());
            return saved;
        }

        private static void ApplyAction(FeedbackInput input)
        {
            switch (input.Action)
            {
                case null: break;
                case "relevant": input.Relevance = RelevanceRatingEnum.Relevant; break;
                case "somewhat": input.Relevance = RelevanceRatingEnum.SomewhatRelevant; break;
                case "not-relevant": input.Relevance = RelevanceRatingEnum.NotRelevant; break;
                case "accurate": input.Accuracy = AccuracyRatingEnum.Accurate; break;
                case "partially": input.Accuracy = AccuracyRatingEnum.PartiallyAccurate; break;
                case "inaccurate": input.Accuracy = AccuracyRatingEnum.Inaccurate; break;
                case "too-early": input.Accuracy = AccuracyRatingEnum.TooEarly; break;
                case "useful": input.Usefulness = UsefulnessRatingEnum.Useful; break;
                case "not-useful": input.Usefulness = UsefulnessRatingEnum.NotUseful; break;
                case "done": input.Usefulness = UsefulnessRatingEnum.Useful; break;
                case "snooze": break;
                case "apply": break;
                case "dismiss": break;
                default: throw new ArgumentException("Unknown action '" + input.Action + "'.", nameof(input));
            }

            bool decision = input.Action == "apply" || input.Action == "dismiss";
            if (decision && input.TargetType != FeedbackTargetTypeEnum.LearnedPreference) throw new ArgumentException("Apply and dismiss are for learned preference proposals.", nameof(input));
            if (input.Action != null && !decision && input.TargetType == FeedbackTargetTypeEnum.LearnedPreference)
                throw new ArgumentException("Use apply or dismiss for a learned preference proposal, or send a rating with a reason.", nameof(input));
            bool fits = input.TargetType switch
            {
                FeedbackTargetTypeEnum.LearnedPreference => !input.Accuracy.HasValue && !input.Usefulness.HasValue,
                FeedbackTargetTypeEnum.Prediction => !input.Usefulness.HasValue,
                FeedbackTargetTypeEnum.Suggestion => !input.Accuracy.HasValue && !input.Relevance.HasValue,
                _ => !input.Accuracy.HasValue && !input.Usefulness.HasValue && input.Action != "snooze" && input.Action != "done"
            };
            if (!fits) throw new ArgumentException("That rating does not apply to a " + input.TargetType + ".", nameof(input));
        }

        private async Task<string?> TargetTopicAsync(FeedbackInput input, CancellationToken token)
        {
            switch (input.TargetType)
            {
                case FeedbackTargetTypeEnum.Finding:
                    Finding f = await _Database.Findings.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) ?? throw NotFound();
                    if (f.UserId != input.UserId) throw NotFound();
                    return f.TopicId;
                case FeedbackTargetTypeEnum.Prediction:
                    Prediction p = await _Database.Predictions.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) ?? throw NotFound();
                    if (p.UserId != input.UserId) throw NotFound();
                    return p.TopicId;
                case FeedbackTargetTypeEnum.Suggestion:
                    Suggestion s = await _Database.Suggestions.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) ?? throw NotFound();
                    if (s.UserId != input.UserId) throw NotFound();
                    return s.TopicId;
                case FeedbackTargetTypeEnum.Briefing:
                    Briefing b = await _Database.Briefings.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) ?? throw NotFound();
                    if (b.UserId != input.UserId) throw NotFound();
                    return b.TopicId;
                case FeedbackTargetTypeEnum.BriefingSection:
                    BriefingSection sec = await _Database.BriefingSections.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) ?? throw NotFound();
                    Briefing owner = await _Database.Briefings.ReadAsync(input.TenantId, sec.BriefingId, token).ConfigureAwait(false) ?? throw NotFound();
                    if (owner.UserId != input.UserId) throw NotFound();
                    return owner.TopicId;
                case FeedbackTargetTypeEnum.LearnedPreference:
                    LearnedPreference lp = await _Database.LearnedPreferences.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) ?? throw NotFound();
                    if (lp.UserId != input.UserId) throw NotFound();
                    return lp.TopicId;
                case FeedbackTargetTypeEnum.SourceItem:
                    if (await _Database.SourceItems.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false) == null) throw NotFound();
                    return null;
                default:
                    return null;
            }
        }

        private async Task SideEffectsAsync(FeedbackInput input, CancellationToken token)
        {
            if (input.TargetType == FeedbackTargetTypeEnum.Prediction && input.Accuracy.HasValue && input.Accuracy.Value != AccuracyRatingEnum.TooEarly)
            {
                Prediction p = (await _Database.Predictions.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false))!;
                p.Status = input.Accuracy.Value == AccuracyRatingEnum.Accurate ? PredictionStatusEnum.Accurate
                    : input.Accuracy.Value == AccuracyRatingEnum.PartiallyAccurate ? PredictionStatusEnum.PartiallyAccurate : PredictionStatusEnum.Inaccurate;
                double outcome = p.Status == PredictionStatusEnum.Accurate ? 1.0 : p.Status == PredictionStatusEnum.PartiallyAccurate ? 0.5 : 0.0;
                p.BrierScore = Math.Round(Math.Pow(p.Confidence - outcome, 2), 4);
                p.ResolvedUtc = DateTime.UtcNow;
                p.ResolvedBy = "User";
                p.ResolutionNote = String.IsNullOrWhiteSpace(input.Reason) ? "Graded from " + input.Channel.ToString().ToLowerInvariant() + "." : input.Reason.Trim();
                await _Database.Predictions.UpdateAsync(p, token).ConfigureAwait(false);
            }

            if (input.TargetType == FeedbackTargetTypeEnum.LearnedPreference && (input.Action == "apply" || input.Action == "dismiss"))
            {
                LearnedPreference lp = (await _Database.LearnedPreferences.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false))!;
                lp.Status = input.Action == "apply" ? PreferenceStatusEnum.Active : PreferenceStatusEnum.Rejected;
                lp.DecidedUtc = DateTime.UtcNow;
                await _Database.LearnedPreferences.UpdateAsync(lp, token).ConfigureAwait(false);
            }

            if (input.TargetType == FeedbackTargetTypeEnum.Suggestion && (input.Action == "done" || input.Action == "snooze"))
            {
                Suggestion s = (await _Database.Suggestions.ReadAsync(input.TenantId, input.TargetId, token).ConfigureAwait(false))!;
                s.Status = input.Action == "done" ? SuggestionStatusEnum.Done : SuggestionStatusEnum.Snoozed;
                s.SnoozedUntilUtc = input.Action == "snooze" ? DateTime.UtcNow.AddDays(7) : null;
                s.StatusChangedUtc = DateTime.UtcNow;
                await _Database.Suggestions.UpdateAsync(s, token).ConfigureAwait(false);
            }
        }

        private static System.Collections.Generic.KeyNotFoundException NotFound()
        {
            return new System.Collections.Generic.KeyNotFoundException("The item was not found.");
        }
    }
}
