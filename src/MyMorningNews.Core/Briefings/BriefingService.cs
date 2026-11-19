namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Scheduling;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Storage;
    using SyslogLogging;

    /// <summary>
    /// Briefing lifecycle (plan sections 21.2 and 21.3): single-run briefings and one-time reports, edition briefings
    /// queued by schedule or on demand (with fresh research first), the composition queue, previews, the seeded
    /// Morning News edition, and deletion. Thread safety: safe for concurrent use; queue claims use row locks.
    /// </summary>
    public class BriefingService : IEditionTrigger
    {
        #region Public-Members

        /// <summary>
        /// Name of the seeded edition.
        /// </summary>
        public const string DefaultEditionName = "Morning News";

        /// <summary>
        /// Composer.
        /// </summary>
        public BriefingComposer Composer { get; }

        /// <summary>
        /// Templates.
        /// </summary>
        public BriefingTemplateService Templates { get; }

        /// <summary>
        /// Audio service; when set, ready briefings with audio turned on are queued for audio.
        /// </summary>
        public Audio.AudioService? Audio { get; set; } = null;

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly SchedulingService _Scheduling;
        private readonly ArtifactService _Artifacts;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private const string _Header = "[BriefingService] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="composer">Composer.</param>
        /// <param name="templates">Templates.</param>
        /// <param name="scheduling">Scheduling (fresh research and edition schedules).</param>
        /// <param name="artifacts">Artifact storage.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public BriefingService(DatabaseDriverBase database, BriefingComposer composer, BriefingTemplateService templates, SchedulingService scheduling, ArtifactService artifacts, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            Composer = composer ?? throw new ArgumentNullException(nameof(composer));
            Templates = templates ?? throw new ArgumentNullException(nameof(templates));
            _Scheduling = scheduling ?? throw new ArgumentNullException(nameof(scheduling));
            _Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Compose the briefing for one run: the topic template or Run Briefing, or the One-time Report for one-time topics
        /// (which also links the briefing to its request).
        /// </summary>
        /// <param name="run">Run.</param>
        /// <param name="topic">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Composed briefing.</returns>
        public async Task<Briefing> ComposeRunBriefingAsync(Run run, Topic topic, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(run);
            ArgumentNullException.ThrowIfNull(topic);
            ResearchRequest? request = String.IsNullOrEmpty(run.ResearchRequestId) ? null : await _Database.ResearchRequests.ReadAsync(run.TenantId, run.ResearchRequestId, token).ConfigureAwait(false);
            bool oneTime = topic.IsOneTime || request != null;
            ResolvedTemplate template = await Templates.ResolveAsync(run.TenantId, topic.BriefingTemplateId, oneTime ? DefaultTemplateCatalog.OneTimeReport : DefaultTemplateCatalog.RunBriefing, token).ConfigureAwait(false);
            User? reader = await _Database.Users.ReadAsync(run.TenantId, topic.UserId, token).ConfigureAwait(false);
            Briefing b = await _Database.Briefings.CreateAsync(new Briefing
            {
                TenantId = run.TenantId,
                UserId = topic.UserId,
                Kind = oneTime ? BriefingKindEnum.OneTime : BriefingKindEnum.Run,
                TopicId = topic.Id,
                RunId = run.Id,
                ResearchRequestId = request?.Id,
                ScheduleId = run.ScheduleId,
                Title = topic.Name,
                Status = BriefingStatusEnum.Composing,
                IsDryRun = run.IsDryRun
            }, token).ConfigureAwait(false);
            ComposeScope scope = new ComposeScope
            {
                TenantId = run.TenantId,
                UserId = topic.UserId,
                ReaderName = ReaderName(reader),
                TimeZone = reader?.TimeZone ?? "UTC",
                Topics = new List<Topic> { topic },
                RunId = run.Id,
                PeriodStartUtc = run.StartedUtc ?? run.QueuedUtc,
                PeriodEndUtc = DateTime.UtcNow,
                Template = template,
                IsDryRun = run.IsDryRun
            };
            Briefing composed = await ComposeOrFailAsync(b, scope, token).ConfigureAwait(false);
            if (request != null && composed.Status == BriefingStatusEnum.Ready)
            {
                request.BriefingId = composed.Id;
                await _Database.ResearchRequests.UpdateAsync(request, token).ConfigureAwait(false);
            }

            bool wantsAudio = request != null
                ? request.Deliverables.Any(d => String.Equals(d, "Podcast", StringComparison.OrdinalIgnoreCase))
                : template.Definition.Audio.Enabled;
            if (wantsAudio) await QueueAudioAsync(composed, request != null ? MediaKindEnum.PodcastEpisode : MediaKindEnum.BriefingAudio, null, token).ConfigureAwait(false);
            return composed;
        }

        /// <inheritdoc />
        public async Task<bool> FireEditionAsync(Schedule schedule, DateTime nowUtc, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            Edition? edition = await _Database.Editions.ReadAsync(schedule.TenantId, schedule.OwnerId, token).ConfigureAwait(false);
            if (edition == null || !edition.Enabled) return false;
            if (await _Database.Briefings.HasActiveForEditionAsync(edition.TenantId, edition.Id, token).ConfigureAwait(false)) return false;
            await QueueEditionAsync(edition, schedule.Id, nowUtc, token).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Queue an edition briefing. With fresh research on, the edition topics are run first and composition waits
        /// until they finish or FreshResearchLeadMinutes pass, whichever is sooner.
        /// </summary>
        /// <param name="edition">Edition.</param>
        /// <param name="scheduleId">Triggering schedule, or null for compose now.</param>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Queued briefing.</returns>
        /// <exception cref="ConflictException">The edition already has a briefing queued or composing.</exception>
        public async Task<Briefing> QueueEditionAsync(Edition edition, string? scheduleId, DateTime nowUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(edition);
            if (await _Database.Briefings.HasActiveForEditionAsync(edition.TenantId, edition.Id, token).ConfigureAwait(false))
                throw new ConflictException("This edition already has a briefing on the way.");
            int queuedRuns = 0;
            if (edition.FreshResearchBeforeCompose)
                foreach (Topic t in await EditionTopicsAsync(edition, token).ConfigureAwait(false))
                    if (await _Scheduling.EnqueueEditionResearchAsync(t, edition.UserId, scheduleId, nowUtc, token).ConfigureAwait(false) != null) queuedRuns++;
            return await _Database.Briefings.CreateAsync(new Briefing
            {
                TenantId = edition.TenantId,
                UserId = edition.UserId,
                Kind = BriefingKindEnum.Edition,
                EditionId = edition.Id,
                ScheduleId = scheduleId,
                Title = edition.Name,
                Status = BriefingStatusEnum.Queued,
                ComposeAfterUtc = nowUtc,
                ResearchDeadlineUtc = queuedRuns > 0 ? nowUtc.AddMinutes(edition.FreshResearchLeadMinutes) : null
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// One queue pass: fail abandoned compositions, then compose claimed edition briefings whose fresh research has
        /// finished (or whose wait ran out). Briefings still waiting are put back.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Briefings composed or failed in this pass.</returns>
        public async Task<int> ProcessQueuedAsync(DateTime nowUtc, CancellationToken token = default)
        {
            await _Database.Briefings.FailAbandonedAsync(nowUtc, token).ConfigureAwait(false);
            List<Briefing> claimed = await _Database.Briefings.ClaimQueuedAsync(nowUtc, nowUtc.AddMinutes(30), 5, token).ConfigureAwait(false);
            int done = 0;
            foreach (Briefing b in claimed)
            {
                token.ThrowIfCancellationRequested();
                Edition? edition = b.EditionId == null ? null : await _Database.Editions.ReadAsync(b.TenantId, b.EditionId, token).ConfigureAwait(false);
                if (edition == null)
                {
                    b.Status = BriefingStatusEnum.Failed;
                    b.ErrorMessage = "The edition no longer exists.";
                    await _Database.Briefings.UpdateAsync(b, token).ConfigureAwait(false);
                    done++;
                    continue;
                }

                List<Topic> topics = await EditionTopicsAsync(edition, token).ConfigureAwait(false);
                if (b.ResearchDeadlineUtc.HasValue && nowUtc < b.ResearchDeadlineUtc.Value && await AnyActiveRunAsync(topics, token).ConfigureAwait(false))
                {
                    b.Status = BriefingStatusEnum.Queued;
                    b.ComposeAfterUtc = nowUtc.AddSeconds(20);
                    b.LockedUntilUtc = null;
                    await _Database.Briefings.UpdateAsync(b, token).ConfigureAwait(false);
                    continue;
                }

                await ComposeEditionAsync(b, edition, topics, nowUtc, token).ConfigureAwait(false);
                done++;
            }

            return done;
        }

        /// <summary>
        /// Compose an edition now without saving (preview). Uses the latest data; model calls are marked as dry runs.
        /// </summary>
        /// <param name="edition">Edition.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Unsaved briefing with Markdown.</returns>
        public async Task<Briefing> PreviewEditionAsync(Edition edition, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(edition);
            DateTime now = DateTime.UtcNow;
            Briefing b = new Briefing { TenantId = edition.TenantId, UserId = edition.UserId, Kind = BriefingKindEnum.Edition, EditionId = edition.Id, Title = edition.Name, IsDryRun = true };
            ComposeScope scope = await EditionScopeAsync(edition, await EditionTopicsAsync(edition, token).ConfigureAwait(false), now, token).ConfigureAwait(false);
            scope.Save = false;
            return await Composer.ComposeAsync(b, scope, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Topics an edition covers: its listed topics in order, or, when none are listed, all of the owner's active recurring topics.
        /// </summary>
        /// <param name="edition">Edition.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Active topics.</returns>
        public async Task<List<Topic>> EditionTopicsAsync(Edition edition, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(edition);
            List<EditionTopic> links = (await _Database.EditionTopics.EnumerateAsync(new EditionTopicFilter { TenantId = edition.TenantId, EditionId = edition.Id, PageSize = 200 }, token).ConfigureAwait(false)).Items
                .OrderBy(l => l.SortOrder).ToList();
            List<Topic> topics = new List<Topic>();
            if (links.Count == 0)
            {
                topics = (await _Database.Topics.EnumerateAsync(new TopicFilter { TenantId = edition.TenantId, UserId = edition.UserId, PageSize = 500 }, token).ConfigureAwait(false)).Items
                    .Where(t => !t.IsOneTime && t.Status == TopicStatusEnum.Active).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
                return topics;
            }

            foreach (EditionTopic l in links)
            {
                Topic? t = await _Database.Topics.ReadAsync(edition.TenantId, l.TopicId, token).ConfigureAwait(false);
                if (t != null && t.Status == TopicStatusEnum.Active) topics.Add(t);
            }

            return topics;
        }

        /// <summary>
        /// Replace an edition's topic list (an empty list means all active recurring topics).
        /// </summary>
        /// <param name="edition">Edition.</param>
        /// <param name="topicIds">Topic IDs in order; each must belong to the edition owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Links.</returns>
        /// <exception cref="ArgumentException">A topic is missing or belongs to someone else.</exception>
        public async Task<List<EditionTopic>> SetTopicsAsync(Edition edition, List<string> topicIds, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(edition);
            ArgumentNullException.ThrowIfNull(topicIds);
            List<string> ids = topicIds.Where(i => !String.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct(StringComparer.Ordinal).ToList();
            foreach (string id in ids)
            {
                Topic? t = await _Database.Topics.ReadAsync(edition.TenantId, id, token).ConfigureAwait(false);
                if (t == null || t.UserId != edition.UserId) throw new ArgumentException("Topic " + id + " was not found among the edition owner's topics.", nameof(topicIds));
            }

            foreach (EditionTopic old in (await _Database.EditionTopics.EnumerateAsync(new EditionTopicFilter { TenantId = edition.TenantId, EditionId = edition.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items)
                await _Database.EditionTopics.DeleteAsync(edition.TenantId, old.Id, token).ConfigureAwait(false);
            List<EditionTopic> links = new List<EditionTopic>();
            for (int i = 0; i < ids.Count; i++)
                links.Add(await _Database.EditionTopics.CreateAsync(new EditionTopic { TenantId = edition.TenantId, EditionId = edition.Id, TopicId = ids[i], SortOrder = i }, token).ConfigureAwait(false));
            return links;
        }

        /// <summary>
        /// Create the Morning News edition (daily at 06:30 in the user's time zone, all topics) when the user has no editions.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The new edition, or null when the user already has one.</returns>
        public async Task<Edition?> EnsureDefaultEditionAsync(string tenantId, string userId, CancellationToken token = default)
        {
            long count = await _Database.Editions.CountAsync(new EditionFilter { TenantId = tenantId, UserId = userId }, token).ConfigureAwait(false);
            if (count > 0) return null;
            User? user = await _Database.Users.ReadAsync(tenantId, userId, token).ConfigureAwait(false);
            if (user == null) return null;
            Edition edition = await _Database.Editions.CreateAsync(new Edition
            {
                TenantId = tenantId,
                UserId = userId,
                Name = DefaultEditionName,
                Description = "Your daily briefing across all of your active topics.",
                AudioEnabled = true,
                EmailEnabled = true
            }, token).ConfigureAwait(false);
            await _Scheduling.SaveAsync(new Schedule
            {
                TenantId = tenantId,
                OwnerType = ScheduleOwnerTypeEnum.Edition,
                OwnerId = edition.Id,
                UserId = userId,
                Name = DefaultEditionName,
                IntervalType = IntervalTypeEnum.DaysOfWeek,
                DaysOfWeekMask = 127,
                TimesOfDay = new List<string> { "06:30" },
                TimeZone = ValidTimeZone(user.TimeZone)
            }, true, DateTime.UtcNow, token).ConfigureAwait(false);
            return edition;
        }

        /// <summary>
        /// Seed Morning News for every active user of a tenant that has no editions at all (first start after upgrading).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Editions created.</returns>
        public async Task<int> BackfillDefaultEditionsAsync(string tenantId, CancellationToken token = default)
        {
            if (await _Database.Editions.CountAsync(new EditionFilter { TenantId = tenantId }, token).ConfigureAwait(false) > 0) return 0;
            int created = 0;
            foreach (User u in (await _Database.Users.EnumerateAsync(new UserFilter { TenantId = tenantId, PageSize = 1000 }, token).ConfigureAwait(false)).Items.Where(u => u.Active))
                if (await EnsureDefaultEditionAsync(tenantId, u.Id, token).ConfigureAwait(false) != null) created++;
            if (created > 0) _Logging.Info(_Header + "seeded " + created + " Morning News editions for tenant " + tenantId);
            return created;
        }

        /// <summary>
        /// Delete an edition with its schedules and topic links. Its briefings stay in history.
        /// </summary>
        /// <param name="edition">Edition.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteEditionAsync(Edition edition, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(edition);
            foreach (Schedule s in (await _Database.Schedules.EnumerateAsync(new ScheduleFilter { TenantId = edition.TenantId, OwnerType = ScheduleOwnerTypeEnum.Edition, OwnerId = edition.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items)
                await _Database.Schedules.DeleteAsync(edition.TenantId, s.Id, token).ConfigureAwait(false);
            foreach (EditionTopic l in (await _Database.EditionTopics.EnumerateAsync(new EditionTopicFilter { TenantId = edition.TenantId, EditionId = edition.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items)
                await _Database.EditionTopics.DeleteAsync(edition.TenantId, l.Id, token).ConfigureAwait(false);
            await _Database.Editions.DeleteAsync(edition.TenantId, edition.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a briefing with its sections, items, and stored files.
        /// </summary>
        /// <param name="briefing">Briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteBriefingAsync(Briefing briefing, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            foreach (Artifact a in (await _Database.Artifacts.EnumerateAsync(new ArtifactFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items)
            {
                await _Artifacts.DeleteContentAsync(a, token).ConfigureAwait(false);
                await _Database.Artifacts.DeleteAsync(a.TenantId, a.Id, token).ConfigureAwait(false);
            }

            foreach (BriefingItem i in (await _Database.BriefingItems.EnumerateAsync(new BriefingItemFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Items)
                await _Database.BriefingItems.DeleteAsync(i.TenantId, i.Id, token).ConfigureAwait(false);
            foreach (BriefingSection s in (await _Database.BriefingSections.EnumerateAsync(new BriefingSectionFilter { TenantId = briefing.TenantId, BriefingId = briefing.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items)
                await _Database.BriefingSections.DeleteAsync(s.TenantId, s.Id, token).ConfigureAwait(false);
            await _Database.Briefings.DeleteAsync(briefing.TenantId, briefing.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete the stored file content of every artifact that belongs to a topic or its briefings (before the topic is deleted).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Files removed.</returns>
        public async Task<int> DeleteTopicFilesAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<Artifact> files = (await _Database.Artifacts.EnumerateAsync(new ArtifactFilter { TenantId = tenantId, TopicId = topicId, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            foreach (Briefing b in (await _Database.Briefings.EnumerateAsync(new BriefingFilter { TenantId = tenantId, TopicId = topicId, PageSize = 1000 }, token).ConfigureAwait(false)).Items)
                files.AddRange((await _Database.Artifacts.EnumerateAsync(new ArtifactFilter { TenantId = tenantId, BriefingId = b.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items);
            int removed = 0;
            foreach (Artifact a in files.Where(f => seen.Add(f.Id)))
            {
                await _Artifacts.DeleteContentAsync(a, token).ConfigureAwait(false);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Store fresh Markdown and HTML files for a briefing whose files expired, from its saved Markdown.
        /// </summary>
        /// <param name="briefing">Ready briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated briefing.</returns>
        /// <exception cref="ConflictException">The briefing is not ready.</exception>
        public async Task<Briefing> RegenerateFilesAsync(Briefing briefing, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            if (briefing.Status != BriefingStatusEnum.Ready || String.IsNullOrEmpty(briefing.Markdown)) throw new ConflictException("Only ready briefings have files to regenerate.");
            await Composer.RenderFilesAsync(briefing, token).ConfigureAwait(false);
            return await _Database.Briefings.UpdateAsync(briefing, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task ComposeEditionAsync(Briefing b, Edition edition, List<Topic> topics, DateTime nowUtc, CancellationToken token)
        {
            ComposeScope scope = await EditionScopeAsync(edition, topics, nowUtc, token).ConfigureAwait(false);
            Briefing composed = await ComposeOrFailAsync(b, scope, token).ConfigureAwait(false);
            if (composed.Status == BriefingStatusEnum.Ready)
            {
                edition.LastComposedUtc = composed.ComposedUtc ?? nowUtc;
                await _Database.Editions.UpdateAsync(edition, token).ConfigureAwait(false);
                if (edition.AudioEnabled) await QueueAudioAsync(composed, MediaKindEnum.BriefingAudio, edition.VoiceProfileId, token).ConfigureAwait(false);
            }
        }

        private async Task<ComposeScope> EditionScopeAsync(Edition edition, List<Topic> topics, DateTime nowUtc, CancellationToken token)
        {
            User? reader = await _Database.Users.ReadAsync(edition.TenantId, edition.UserId, token).ConfigureAwait(false);
            DateTime cap = nowUtc.AddHours(-edition.LookbackHours);
            DateTime start = edition.LookbackMode == LookbackModeEnum.SinceLastEdition && edition.LastComposedUtc.HasValue && edition.LastComposedUtc.Value > cap ? edition.LastComposedUtc.Value : cap;
            return new ComposeScope
            {
                TenantId = edition.TenantId,
                UserId = edition.UserId,
                ReaderName = ReaderName(reader),
                TimeZone = ValidTimeZone(reader?.TimeZone),
                Topics = topics,
                PeriodStartUtc = start,
                PeriodEndUtc = nowUtc,
                EditionName = edition.Name,
                Template = await Templates.ResolveAsync(edition.TenantId, edition.BriefingTemplateId, DefaultTemplateCatalog.MorningNews, token).ConfigureAwait(false)
            };
        }

        private async Task<Briefing> ComposeOrFailAsync(Briefing b, ComposeScope scope, CancellationToken token)
        {
            try
            {
                return await Composer.ComposeAsync(b, scope, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + "briefing " + b.Id + " failed: " + ex.Message);
                b.Status = BriefingStatusEnum.Failed;
                b.ErrorMessage = ex.Message;
                b.LockedUntilUtc = null;
                return await _Database.Briefings.UpdateAsync(b, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task QueueAudioAsync(Briefing b, MediaKindEnum kind, string? voiceProfileId, CancellationToken token)
        {
            if (Audio == null || b.Status != BriefingStatusEnum.Ready || b.IsDryRun) return;
            try
            {
                await Audio.QueueForBriefingAsync(b, kind, voiceProfileId, false, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Logging.Warn(_Header + "could not queue audio for briefing " + b.Id + ": " + ex.Message);
            }
        }

        private async Task<bool> AnyActiveRunAsync(List<Topic> topics, CancellationToken token)
        {
            foreach (Topic t in topics)
                if (await _Database.Runs.HasActiveRunAsync(t.TenantId, t.Id, token).ConfigureAwait(false)) return true;
            return false;
        }

        private static string? ReaderName(User? user)
        {
            if (user == null) return null;
            string name = ((user.FirstName ?? String.Empty) + " " + (user.LastName ?? String.Empty)).Trim();
            return name.Length == 0 ? null : name;
        }

        private static string ValidTimeZone(string? tz)
        {
            if (String.IsNullOrWhiteSpace(tz)) return "UTC";
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(tz);
                return tz;
            }
            catch (TimeZoneNotFoundException)
            {
                return "UTC";
            }
            catch (InvalidTimeZoneException)
            {
                return "UTC";
            }
        }

        #endregion
    }
}
