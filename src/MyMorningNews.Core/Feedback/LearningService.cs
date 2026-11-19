namespace MyMorningNews.Core.Feedback
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Pipeline;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Learns from feedback (plan section 18): tags free-text reasons into themes, distills preference proposals backed by
    /// at least three feedback items, and supplies feedback exemplars and active preferences to the pipeline prompts.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class LearningService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly ModelGateway _Models;
        private readonly PromptService _Prompts;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private const string _Header = "[LearningService] ";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="prompts">Prompts.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public LearningService(DatabaseDriverBase database, ModelGateway models, PromptService prompts, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// One worker pass: learn for every user and topic with enough new feedback.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Preferences proposed.</returns>
        public async Task<int> ProcessDueAsync(CancellationToken token = default)
        {
            if (!_Settings.Learning.Enabled) return 0;
            int proposed = 0;
            foreach (LearningCandidate c in await _Database.Feedback.LearningCandidatesAsync(_Settings.Learning.FeedbackPerPass, 50, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                proposed += await LearnAsync(c.TenantId, c.UserId, c.TopicId, c.NewestUtc, token).ConfigureAwait(false);
            }

            return proposed;
        }

        /// <summary>
        /// Tag reasons and distill proposals for one user and topic, then move the checkpoint. Model failures are logged.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="newestUtc">Newest feedback considered.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Preferences proposed (or made active with AutoApply).</returns>
        public async Task<int> LearnAsync(string tenantId, string userId, string topicId, DateTime newestUtc, CancellationToken token = default)
        {
            Topic? topic = await _Database.Topics.ReadAsync(tenantId, topicId, token).ConfigureAwait(false);
            int created = 0;
            if (topic != null)
            {
                List<Models.Feedback> items = (await _Database.Feedback.EnumerateAsync(new FeedbackFilter { TenantId = tenantId, UserId = userId, TopicId = topicId, IsCurrent = true, PageSize = 100 }, token).ConfigureAwait(false)).Items
                    .OrderByDescending(f => f.CreatedUtc).ToList();
                Dictionary<string, string> titles = await TitlesAsync(tenantId, items, token).ConfigureAwait(false);
                try
                {
                    await TagAsync(topic, items, titles, token).ConfigureAwait(false);
                    created = await DistillAsync(topic, userId, items, titles, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ModelCallException || ex is StructuredOutputException)
                {
                    _Logging.Warn(_Header + "learning for topic " + topicId + " skipped: " + ex.Message);
                }
            }

            LearningCheckpoint? cp = (await _Database.LearningCheckpoints.EnumerateAsync(new LearningCheckpointFilter { TenantId = tenantId, UserId = userId, TopicId = topicId, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
            if (cp == null)
            {
                await _Database.LearningCheckpoints.CreateAsync(new LearningCheckpoint { TenantId = tenantId, UserId = userId, TopicId = topicId, LastFeedbackUtc = newestUtc, LastRunUtc = DateTime.UtcNow, Proposed = created }, token).ConfigureAwait(false);
            }
            else
            {
                cp.LastFeedbackUtc = newestUtc;
                cp.LastRunUtc = DateTime.UtcNow;
                cp.Proposed += created;
                await _Database.LearningCheckpoints.UpdateAsync(cp, token).ConfigureAwait(false);
            }

            return created;
        }

        /// <summary>
        /// Feedback exemplars for prompts: up to five Relevant and five Not relevant findings (newest), each with its why.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Lines.</returns>
        public async Task<List<string>> ExemplarsAsync(string tenantId, string userId, string topicId, CancellationToken token = default)
        {
            List<Models.Feedback> rated = (await _Database.Feedback.EnumerateAsync(new FeedbackFilter { TenantId = tenantId, UserId = userId, TopicId = topicId, TargetType = FeedbackTargetTypeEnum.Finding, IsCurrent = true, PageSize = 200 }, token).ConfigureAwait(false)).Items
                .Where(f => f.Relevance.HasValue).OrderByDescending(f => f.CreatedUtc).ToList();
            List<Models.Feedback> pick = rated.Where(f => f.Relevance == RelevanceRatingEnum.Relevant).Take(5).Concat(rated.Where(f => f.Relevance == RelevanceRatingEnum.NotRelevant).Take(5)).ToList();
            Dictionary<string, string> titles = await TitlesAsync(tenantId, pick, token).ConfigureAwait(false);
            return pick.Select(f => (f.Relevance == RelevanceRatingEnum.Relevant ? "Relevant" : "Not relevant") + ": “" + (titles.TryGetValue(f.TargetId, out string? t) ? t : f.TargetId) + "”" +
                (String.IsNullOrWhiteSpace(f.Reason) ? String.Empty : " because " + f.Reason!.Trim())).ToList();
        }

        /// <summary>
        /// Active preferences for a topic (topic-specific and all-topic), as prompt lines.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Lines such as "Avoid: funding news under 100 million dollars".</returns>
        public async Task<List<string>> ActivePreferencesAsync(string tenantId, string userId, string topicId, CancellationToken token = default)
        {
            List<LearnedPreference> prefs = (await _Database.LearnedPreferences.EnumerateAsync(new LearnedPreferenceFilter { TenantId = tenantId, UserId = userId, Status = PreferenceStatusEnum.Active, PageSize = 200 }, token).ConfigureAwait(false)).Items
                .Where(p => p.TopicId == null || p.TopicId == topicId).OrderBy(p => p.Kind).ThenBy(p => p.CreatedUtc).ToList();
            return prefs.Select(p => Label(p.Kind) + ": " + p.Statement.Trim()).ToList();
        }

        /// <summary>
        /// Prompt label for a preference kind.
        /// </summary>
        /// <param name="kind">Kind.</param>
        /// <returns>Label.</returns>
        public static string Label(PreferenceKindEnum kind)
        {
            switch (kind)
            {
                case PreferenceKindEnum.Avoid: return "Avoid";
                case PreferenceKindEnum.PreferSource: return "Prefer source";
                case PreferenceKindEnum.AvoidSource: return "Avoid source";
                case PreferenceKindEnum.StyleNote: return "Style";
                default: return "Prefer";
            }
        }

        private async Task TagAsync(Topic topic, List<Models.Feedback> items, Dictionary<string, string> titles, CancellationToken token)
        {
            List<FeedbackReasonTag> existing = (await _Database.FeedbackReasonTags.EnumerateAsync(new FeedbackReasonTagFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            HashSet<string> tagged = new HashSet<string>(existing.Select(t => t.FeedbackId), StringComparer.Ordinal);
            List<Models.Feedback> untagged = items.Where(f => !String.IsNullOrWhiteSpace(f.Reason) && !tagged.Contains(f.Id)).Take(40).ToList();
            if (untagged.Count == 0) return;
            StructuredCallResult<ReasonTagResult> result = await CallAsync<ReasonTagResult>(topic, "feedback.tag-reasons", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "feedback.items", untagged.Select(f => Line(f, titles, null)).ToList() },
                { "tags.existing", existing.Select(t => t.Tag).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).Take(100).ToList() }
            }, token).ConfigureAwait(false);
            HashSet<string> valid = new HashSet<string>(untagged.Select(f => f.Id), StringComparer.Ordinal);
            foreach (TaggedReason r in result.Value.Items.Where(r => valid.Contains(r.FeedbackId.Trim().Trim('[', ']'))))
                foreach (string tag in r.Tags)
                    await _Database.FeedbackReasonTags.CreateAsync(new FeedbackReasonTag { TenantId = topic.TenantId, FeedbackId = r.FeedbackId.Trim().Trim('[', ']'), TopicId = topic.Id, Tag = tag }, token).ConfigureAwait(false);
        }

        private async Task<int> DistillAsync(Topic topic, string userId, List<Models.Feedback> items, Dictionary<string, string> titles, CancellationToken token)
        {
            if (items.Count < 3) return 0;
            List<FeedbackReasonTag> tags = (await _Database.FeedbackReasonTags.EnumerateAsync(new FeedbackReasonTagFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            List<LearnedPreference> existing = (await _Database.LearnedPreferences.EnumerateAsync(new LearnedPreferenceFilter { TenantId = topic.TenantId, UserId = userId, PageSize = 500 }, token).ConfigureAwait(false)).Items
                .Where(p => p.TopicId == null || p.TopicId == topic.Id).ToList();
            StructuredCallResult<PreferenceProposalResult> result = await CallAsync<PreferenceProposalResult>(topic, "feedback.distill-preferences", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "topic.name", topic.Name },
                { "feedback.items", items.Take(60).Select(f => Line(f, titles, tags.Where(t => t.FeedbackId == f.Id).Select(t => t.Tag).ToList())).ToList() },
                { "preferences.existing", existing.Select(p => "[" + p.Status + "] " + Label(p.Kind) + ": " + p.Statement).ToList() }
            }, token).ConfigureAwait(false);

            Dictionary<string, Models.Feedback> byId = items.ToDictionary(f => f.Id, f => f, StringComparer.Ordinal);
            int created = 0;
            foreach (ProposedPreference pp in result.Value.Preferences)
            {
                List<Models.Feedback> support = pp.FeedbackIds.Select(id => id.Trim().Trim('[', ']')).Where(byId.ContainsKey).Distinct(StringComparer.Ordinal).Select(id => byId[id]).ToList();
                if (support.Count < 3) continue;
                if (existing.Any(e => PipelineServices.WordSimilarity(e.Statement, pp.Statement) >= 0.6)) continue;
                LearnedPreference p = await _Database.LearnedPreferences.CreateAsync(new LearnedPreference
                {
                    TenantId = topic.TenantId,
                    UserId = userId,
                    TopicId = topic.Id,
                    Kind = pp.Kind,
                    Statement = pp.Statement.Trim(),
                    Status = _Settings.Learning.AutoApply ? PreferenceStatusEnum.Active : PreferenceStatusEnum.Proposed,
                    Origin = PreferenceOriginEnum.Distilled,
                    SupportCount = support.Count,
                    Confidence = pp.Confidence,
                    LastEvidenceUtc = support.Max(f => f.CreatedUtc),
                    DecidedUtc = _Settings.Learning.AutoApply ? DateTime.UtcNow : null
                }, token).ConfigureAwait(false);
                foreach (Models.Feedback f in support)
                    await _Database.LearnedPreferenceFeedback.CreateAsync(new LearnedPreferenceFeedback { TenantId = topic.TenantId, LearnedPreferenceId = p.Id, FeedbackId = f.Id }, token).ConfigureAwait(false);
                existing.Add(p);
                created++;
            }

            return created;
        }

        private async Task<StructuredCallResult<T>> CallAsync<T>(Topic topic, string key, Dictionary<string, object?> values, CancellationToken token) where T : class, IStructuredResult
        {
            ResolvedPrompt prompt = await _Prompts.ResolveAsync(topic.TenantId, key, topic.Id, token).ConfigureAwait(false);
            RenderedPrompt rendered = PromptService.Render(prompt.Definition, values);
            return await _Models.ChatStructuredAsync<T>(new ModelCallRequest
            {
                TenantId = topic.TenantId, UserId = topic.UserId, TopicId = topic.Id, Task = prompt.Definition.Role, PromptKey = key,
                PromptTemplateVersionId = prompt.Version.Id, SystemPrompt = rendered.System, UserPrompt = rendered.User
            }, token).ConfigureAwait(false);
        }

        private static string Line(Models.Feedback f, Dictionary<string, string> titles, List<string>? tags)
        {
            string rating = f.Relevance?.ToString() ?? f.Accuracy?.ToString() ?? f.Usefulness?.ToString() ?? "Comment";
            string title = titles.TryGetValue(f.TargetId, out string? t) ? t : f.TargetId;
            return "[" + f.Id + "] " + rating + " on " + f.TargetType + " “" + title + "”" + (String.IsNullOrWhiteSpace(f.Reason) ? String.Empty : ": " + f.Reason!.Trim()) +
                (tags != null && tags.Count > 0 ? " (tags: " + String.Join(", ", tags) + ")" : String.Empty);
        }

        private async Task<Dictionary<string, string>> TitlesAsync(string tenantId, List<Models.Feedback> items, CancellationToken token)
        {
            Dictionary<string, string> titles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Models.Feedback f in items)
            {
                if (titles.ContainsKey(f.TargetId)) continue;
                string? title = null;
                if (f.TargetType == FeedbackTargetTypeEnum.Finding)
                {
                    Finding? x = await _Database.Findings.ReadAsync(tenantId, f.TargetId, token).ConfigureAwait(false);
                    if (x != null)
                    {
                        SourceItem? s = x.SourceItemId == null ? null : await _Database.SourceItems.ReadAsync(tenantId, x.SourceItemId, token).ConfigureAwait(false);
                        title = x.Title + (s?.Domain != null ? " (" + s.Domain + ")" : String.Empty);
                    }
                }
                else if (f.TargetType == FeedbackTargetTypeEnum.Prediction) title = (await _Database.Predictions.ReadAsync(tenantId, f.TargetId, token).ConfigureAwait(false))?.Statement;
                else if (f.TargetType == FeedbackTargetTypeEnum.Suggestion) title = (await _Database.Suggestions.ReadAsync(tenantId, f.TargetId, token).ConfigureAwait(false))?.Title;
                if (title != null) titles[f.TargetId] = title;
            }

            return titles;
        }
    }
}
