namespace MyMorningNews.Core.Catalog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Scheduling;
    using MyMorningNews.Core.Services;

    /// <summary>
    /// Makes an administrator's own topic a system-wide briefing: its research setup (brief, scope, dimensions,
    /// constraints, ranking, history, and schedule) is copied to the catalog owner as a new canonical topic with a
    /// catalog publication. The source topic is not changed. Workspace-private settings are left behind because they
    /// do not exist in the catalog workspace: MCP tools, the voice, the briefing template, search provider choices,
    /// email recipients, and a history pinned to one of the source's briefings.
    /// Thread safety: stateless apart from injected services.
    /// </summary>
    public class CatalogPromotionService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly RefinementService _Refinement;
        private readonly TenantDefaultsService _TenantDefaults;
        private readonly SchedulingService _Scheduling;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="refinement">Creates the canonical topic.</param>
        /// <param name="tenantDefaults">Seeds the catalog workspace.</param>
        /// <param name="scheduling">Saves the copied schedule.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CatalogPromotionService(DatabaseDriverBase database, RefinementService refinement, TenantDefaultsService tenantDefaults, SchedulingService scheduling)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Refinement = refinement ?? throw new ArgumentNullException(nameof(refinement));
            _TenantDefaults = tenantDefaults ?? throw new ArgumentNullException(nameof(tenantDefaults));
            _Scheduling = scheduling ?? throw new ArgumentNullException(nameof(scheduling));
        }

        /// <summary>
        /// Copy a topic into the catalog and create its publication.
        /// </summary>
        /// <param name="source">The administrator's topic.</param>
        /// <param name="publication">Publication fields (title, description, category, language, status, free eligibility, order).</param>
        /// <param name="nowUtc">Current time, UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The new publication; its TopicId is the canonical copy.</returns>
        /// <exception cref="ArgumentNullException">source or publication is null.</exception>
        /// <exception cref="ArgumentException">The source is a one-time topic, or it has no research scope yet.</exception>
        public async Task<CatalogPublication> PromoteAsync(Topic source, CatalogPublication publication, DateTime nowUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(publication);
            if (source.IsOneTime) throw new ArgumentException("One-time reports cannot become system briefings. Use a recurring topic.", nameof(source));
            if (String.IsNullOrWhiteSpace(source.Brief)) throw new ArgumentException("Finish the topic's research scope (the wizard) before making it system-wide.", nameof(source));

            User owner = await _Database.CatalogPublications.EnsureOwnerAsync(token).ConfigureAwait(false);
            await _TenantDefaults.EnsureAsync(owner.TenantId, token).ConfigureAwait(false);
            Topic copy = await _Refinement.CreateTopicAsync(owner.TenantId, owner.Id, publication.Title, publication.Description ?? source.Description, false, token).ConfigureAwait(false);
            CopyResearch(source, copy);
            try
            {
                await new HistoryContextService(_Database).ValidateAsync(copy, token).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                copy.HistoryMode = HistoryModeEnum.Off;
                copy.HistorySinceUtc = null;
            }

            copy = await _Database.Topics.UpdateAsync(copy, token).ConfigureAwait(false);
            Dictionary<string, string> dimensionIds = await CopyDimensionsAsync(source, copy, token).ConfigureAwait(false);
            await CopyConstraintsAsync(source, copy, dimensionIds, token).ConfigureAwait(false);
            await CopyScheduleAsync(source, copy, nowUtc, token).ConfigureAwait(false);

            publication.TopicId = copy.Id;
            publication.OwnerTenantId = owner.TenantId;
            if (publication.Status == "Published") publication.PublishedUtc ??= nowUtc;
            return await _Database.CatalogPublications.CreateAsync(publication, token).ConfigureAwait(false);
        }

        private static void CopyResearch(Topic s, Topic t)
        {
            t.Brief = s.Brief;
            t.AudienceLevel = s.AudienceLevel;
            t.Horizon = s.Horizon;
            t.Language = s.Language;
            t.NoveltyPreference = s.NoveltyPreference;
            t.EmailOnRun = false;
            t.EmailWhenEmpty = false;
            t.MaxEmailFindings = s.MaxEmailFindings;
            t.DisplayThreshold = s.DisplayThreshold;
            t.WeightLlm = s.WeightLlm;
            t.WeightFeedback = s.WeightFeedback;
            t.WeightNovelty = s.WeightNovelty;
            t.WeightSource = s.WeightSource;
            t.WeightRecency = s.WeightRecency;
            t.SeedQueries = new List<string>(s.SeedQueries);
            t.AgenticSearch = s.AgenticSearch;
            t.AudioEnabled = s.AudioEnabled;
            t.HistoryMode = s.HistoryMode == HistoryModeEnum.SpecificIssue ? HistoryModeEnum.Off : s.HistoryMode;
            t.HistoryCount = s.HistoryCount;
            t.HistorySinceUtc = s.HistorySinceUtc;
            t.HistoryTokenBudget = s.HistoryTokenBudget;
            t.LastRefinedUtc = s.LastRefinedUtc ?? DateTime.UtcNow;
            t.Status = TopicStatusEnum.Active;
        }

        private async Task<Dictionary<string, string>> CopyDimensionsAsync(Topic s, Topic t, CancellationToken token)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            List<TopicDimension> dims = (await _Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = s.TenantId, TopicId = s.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Objects;
            foreach (TopicDimension d in dims.Where(d => d.Active).OrderBy(d => d.SortOrder))
            {
                TopicDimension created = await _Database.TopicDimensions.CreateAsync(new TopicDimension
                {
                    TenantId = t.TenantId, TopicId = t.Id, Name = d.Name, Description = d.Description, Rationale = d.Rationale, Weight = d.Weight,
                    IncludeTerms = new List<string>(d.IncludeTerms), ExcludeTerms = new List<string>(d.ExcludeTerms), IsLocked = d.IsLocked, Origin = d.Origin, SortOrder = d.SortOrder
                }, token).ConfigureAwait(false);
                map[d.Id] = created.Id;
            }

            if (map.Count == 0)
                await _Database.TopicDimensions.CreateAsync(new TopicDimension { TenantId = t.TenantId, TopicId = t.Id, Name = "Overview", Description = "Research the defined public scope.", Weight = DimensionWeightEnum.High }, token).ConfigureAwait(false);
            return map;
        }

        private async Task CopyConstraintsAsync(Topic s, Topic t, Dictionary<string, string> dimensionIds, CancellationToken token)
        {
            List<TopicConstraint> rows = (await _Database.TopicConstraints.EnumerateAsync(new TopicConstraintFilter { TenantId = s.TenantId, TopicId = s.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Objects;
            foreach (TopicConstraint c in rows.Where(c => c.Active))
            {
                string? dimension = c.DimensionId != null && dimensionIds.TryGetValue(c.DimensionId, out string? mapped) ? mapped : null;
                if (c.DimensionId != null && dimension == null) continue;
                await _Database.TopicConstraints.CreateAsync(new TopicConstraint
                {
                    TenantId = t.TenantId, TopicId = t.Id, DimensionId = dimension, Kind = c.Kind, Value = c.Value, Note = c.Note, IsLocked = c.IsLocked, Origin = c.Origin
                }, token).ConfigureAwait(false);
            }
        }

        private async Task CopyScheduleAsync(Topic s, Topic t, DateTime nowUtc, CancellationToken token)
        {
            Schedule? source = (await _Database.Schedules.EnumerateAsync(new ScheduleFilter { TenantId = s.TenantId, OwnerType = ScheduleOwnerTypeEnum.Topic, OwnerId = s.Id, PageSize = 1 }, token).ConfigureAwait(false)).Objects.FirstOrDefault();
            if (source == null) return;
            Schedule copy = new Schedule
            {
                TenantId = t.TenantId, UserId = t.UserId, OwnerType = ScheduleOwnerTypeEnum.Topic, OwnerId = t.Id, Name = source.Name,
                IntervalType = source.IntervalType, IntervalValue = source.IntervalValue, MinuteOffset = source.MinuteOffset, DaysOfWeekMask = source.DaysOfWeekMask,
                DaysOfMonth = new List<int>(source.DaysOfMonth), TimesOfDay = new List<string>(source.TimesOfDay), CronExpression = source.CronExpression,
                TimeZone = source.TimeZone, StartUtc = source.StartUtc, EndUtc = source.EndUtc, Enabled = source.Enabled, MissedRunPolicy = source.MissedRunPolicy
            };
            await _Scheduling.SaveAsync(copy, true, nowUtc, token).ConfigureAwait(false);
        }
    }
}
