namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Catalog;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>System publications and private subscriptions. Canonical research remains in a protected workspace.</summary>
    public class CatalogRoutes : RouteBase
    {
        private readonly CatalogService _Catalog;

        /// <summary>Create routes using the server dependencies.</summary>
        public CatalogRoutes(ServerContext server) : base(server)
        {
            _Catalog = new CatalogService(server.Database, server.Entitlements);
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string path = "/v1.0/api/catalog";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, path, ListAsync, null, openApiMetadata: Paged(Doc("Browse system briefings", "Catalog").WithParameter(QueryParam("category", typeof(string))).WithParameter(QueryParam("language", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, path + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("Browse system briefings", "Catalog").WithParameter(QueryParam("category", typeof(string))).WithParameter(QueryParam("language", typeof(string)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, path + "/subscriptions", SubscriptionsAsync, null, openApiMetadata: Paged(Doc("My system briefing subscriptions", "Catalog")));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, path + "/subscriptions" + "/enumerate", ctx => EnumerateAsync(ctx, SubscriptionsAsync), null, openApiMetadata: Paged(Doc("My system briefing subscriptions", "Catalog")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, path + "/{id}/subscription", SubscribeAsync, null, openApiMetadata: Doc("Subscribe to a system briefing", "Catalog", "Idempotent. System subscriptions have a separate plan limit from custom topics.").WithRequestBody(Body<CatalogSubscriptionRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, path + "/{id}/subscription", UnsubscribeAsync, null, openApiMetadata: Doc("Unsubscribe from a system briefing", "Catalog"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, path + "/{id}/issues", IssuesAsync, null, openApiMetadata: Paged(Doc("List subscribed system briefing issues", "Catalog")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, path + "/{id}/issues" + "/enumerate", ctx => EnumerateAsync(ctx, IssuesAsync), null, openApiMetadata: Paged(Doc("List subscribed system briefing issues", "Catalog")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, path + "/{id}/issues/{issueId}", IssueAsync, null, openApiMetadata: Doc("Read a subscribed system briefing issue", "Catalog", "Includes comparison: the earlier published issues it was compared with and the changes found (new, changed, corrected, resolved, unchanged, unavailable), without canonical item identifiers."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, path + "/{id}/issues/{issueId}/audio", AudioAsync, null, openApiMetadata: Doc("Stream shared system briefing audio", "Catalog", "Supports byte ranges. Replaying shared audio does not consume personal generation minutes."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, path + "/{id}/issues/{issueId}/progress", ProgressAsync, null, openApiMetadata: Doc("Read personal progress for a shared issue", "Catalog"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, path + "/{id}/issues/{issueId}/progress", SaveProgressAsync, null, openApiMetadata: Doc("Save personal progress for a shared issue", "Catalog").WithRequestBody(Body<BriefingProgressRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, path + "/issues", AllIssuesAsync, null, openApiMetadata: Paged(Doc("Shared issues from all my subscriptions", "Catalog", "Newest first. Content-only projection; suspended or retracted publications are excluded.")).WithParameter(QueryParam("audioOnly", typeof(bool))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, path + "/issues" + "/enumerate", ctx => EnumerateAsync(ctx, AllIssuesAsync), null, openApiMetadata: Paged(Doc("Shared issues from all my subscriptions", "Catalog", "Newest first. Content-only projection; suspended or retracted publications are excluded.")).WithParameter(QueryParam("audioOnly", typeof(bool))).WithRequestBody(Body<EnumerationQuery>()));
            const string admin = "/v1.0/api/admin/catalog";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, admin, AdminListAsync, null, openApiMetadata: Paged(Doc("Manage system publications", "Catalog administration")));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, admin + "/enumerate", ctx => EnumerateAsync(ctx, AdminListAsync), null, openApiMetadata: Paged(Doc("Manage system publications", "Catalog administration")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, admin + "/{id}", AdminReadAsync, null, openApiMetadata: Doc("Read a system publication", "Catalog administration"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, admin + "/{id}/issues", AdminIssuesAsync, null, openApiMetadata: Paged(Doc("Inspect canonical published issues", "Catalog administration")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, admin + "/{id}/issues" + "/enumerate", ctx => EnumerateAsync(ctx, AdminIssuesAsync), null, openApiMetadata: Paged(Doc("Inspect canonical published issues", "Catalog administration")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, admin + "/{id}/research", ResearchAsync, null, openApiMetadata: Doc("Read canonical research and schedule settings", "Catalog administration"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, admin + "/{id}/research", SaveResearchAsync, null, openApiMetadata: Doc("Configure canonical research and schedule", "Catalog administration", "Topic and schedule ownership come exclusively from the publication.").WithRequestBody(Body<CatalogResearchRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, admin + "/{id}/run", RunAsync, null, openApiMetadata: Doc("Research a system publication now", "Catalog administration"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, admin + "/from-topic/{topicId}", PromoteAsync, null, openApiMetadata: Doc("Make one of my topics a system briefing", "Catalog administration",
                "Copies the caller's recurring topic (scope, dimensions, constraints, ranking, history, and schedule) to the catalog owner and creates its publication. " +
                "The caller's topic is unchanged. MCP tools, voice, template, search provider choices, and recipients stay behind.").WithRequestBody(Body<CatalogPublicationRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, admin, CreateAsync, null, openApiMetadata: Doc("Create a canonical system topic and publication", "Catalog administration").WithRequestBody(Body<CatalogPublicationRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, admin + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Publish, retire, retract, or edit a system publication", "Catalog administration", "Requires the current revision. Returns 409 on concurrent edits.").WithRequestBody(Body<CatalogPublicationRequest>()));
        }

        private async Task<bool> MemberAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false)) return false;
            if (await RequireTenantAsync(ctx).ConfigureAwait(false) == null) return false;
            if (!String.IsNullOrEmpty(Context(ctx).UserId)) return true;
            await SendErrorAsync(ctx, 403, "Forbidden", "Sign in as a user to manage briefing subscriptions.").ConfigureAwait(false);
            return false;
        }

        private async Task<bool> AdminAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return false;
            if (Context(ctx).IsAdmin) return true;
            await SendErrorAsync(ctx, 403, "Forbidden", "Only system administrators manage the shared catalog.").ConfigureAwait(false);
            return false;
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await MemberAsync(ctx).ConfigureAwait(false)) return;
            string tenantId = Context(ctx).TenantId!;
            string userId = Context(ctx).UserId!;
            EffectivePlan plan = await _Catalog.ReconcileAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            CatalogPublicationFilter filter = new CatalogPublicationFilter { Active = true, Status = "Published", Category = Query(ctx, "category"), Language = Query(ctx, "language") };
            ApplyPaging(ctx, filter);
            EnumerationResult<CatalogPublication> page = await Server.Database.CatalogPublications.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            List<CatalogEntryResponse> entries = new List<CatalogEntryResponse>();
            foreach (CatalogPublication publication in page.Objects)
            {
                CatalogSubscription? subscription = (await Server.Database.CatalogSubscriptions.EnumerateAsync(new CatalogSubscriptionFilter
                { TenantId = tenantId, UserId = userId, PublicationId = publication.Id, Active = true, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
                entries.Add(new CatalogEntryResponse
                {
                    Id = publication.Id, Title = publication.Title, Description = publication.Description, Category = publication.Category,
                    Language = publication.Language, Status = publication.Status, FreeEligible = publication.FreeEligible,
                    Eligible = !CatalogService.FreeOnly(plan) || publication.FreeEligible, Subscribed = subscription != null, Suspended = subscription?.Suspended ?? false, EmailEnabled = subscription?.EmailEnabled, PushEnabled = subscription?.PushEnabled,
                    PublishedUtc = publication.PublishedUtc, SortOrder = publication.SortOrder
                });
            }
            await SendJsonAsync(ctx, 200, EnumerationResult<CatalogEntryResponse>.Create(entries, filter, page.TotalRecords, page.TotalMs)).ConfigureAwait(false);
        }

        private async Task SubscriptionsAsync(HttpContextBase ctx)
        {
            if (!await MemberAsync(ctx).ConfigureAwait(false)) return;
            await _Catalog.ReconcileAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false);
            CatalogSubscriptionFilter filter = new CatalogSubscriptionFilter { TenantId = Context(ctx).TenantId, UserId = Context(ctx).UserId, Active = true };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.CatalogSubscriptions.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SubscribeAsync(HttpContextBase ctx)
        {
            if (!await MemberAsync(ctx).ConfigureAwait(false)) return;
            CatalogSubscriptionRequest? request = await ReadBodyAsync<CatalogSubscriptionRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            CatalogSubscription subscription = await _Catalog.SubscribeAsync(Context(ctx).TenantId!, Context(ctx).UserId!, Param(ctx, "id")!, request, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, subscription).ConfigureAwait(false);
        }

        private async Task UnsubscribeAsync(HttpContextBase ctx)
        {
            if (!await MemberAsync(ctx).ConfigureAwait(false)) return;
            CatalogSubscription? subscription = (await Server.Database.CatalogSubscriptions.EnumerateAsync(new CatalogSubscriptionFilter
            { TenantId = Context(ctx).TenantId, UserId = Context(ctx).UserId, PublicationId = Param(ctx, "id"), PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
            if (subscription != null)
            {
                subscription.Active = false;
                await Server.Database.CatalogSubscriptions.UpdateAsync(subscription, ctx.Token).ConfigureAwait(false);
            }
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task<CatalogPublication?> ReadableAsync(HttpContextBase ctx)
        {
            if (!await MemberAsync(ctx).ConfigureAwait(false)) return null;
            CatalogPublication? publication = await _Catalog.ReadableAsync(Context(ctx).TenantId!, Context(ctx).UserId!, Param(ctx, "id")!, ctx.Token).ConfigureAwait(false);
            if (publication == null) await SendNotFoundAsync(ctx, "Subscription").ConfigureAwait(false);
            return publication;
        }

        private async Task AllIssuesAsync(HttpContextBase ctx)
        {
            if (!await MemberAsync(ctx).ConfigureAwait(false)) return;
            EffectivePlan plan = await _Catalog.ReconcileAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false);
            EnumerationQuery filter = new EnumerationQuery();
            ApplyPaging(ctx, filter);
            EnumerationResult<CatalogIssueResponse> page = await Server.Database.CatalogSubscriptions.IssuesAsync(Context(ctx).TenantId!, Context(ctx).UserId!, CatalogService.FreeOnly(plan), filter, ctx.Token, QueryBool(ctx, "audioOnly") == true).ConfigureAwait(false);
            foreach (CatalogIssueResponse issue in page.Objects)
            {
                CatalogPublication? publication = await _Catalog.ReadableAsync(Context(ctx).TenantId!, Context(ctx).UserId!, issue.PublicationId, ctx.Token).ConfigureAwait(false);
                if (publication == null) continue;
                MediaAsset? media = await _Catalog.AudioAsync(publication, issue.Id, ctx.Token).ConfigureAwait(false);
                if (media != null)
                {
                    issue.AudioSeconds = media.DurationSeconds;
                    issue.AudioPath = "/v1.0/api/catalog/" + issue.PublicationId + "/issues/" + issue.Id + "/audio";
                }
            }
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task IssuesAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await ReadableAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            await PublicationIssuesAsync(publication, ctx).ConfigureAwait(false);
        }

        private async Task AdminIssuesAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await AdminPublicationAsync(ctx).ConfigureAwait(false);
            if (publication != null) await PublicationIssuesAsync(publication, ctx).ConfigureAwait(false);
        }

        private async Task PublicationIssuesAsync(CatalogPublication publication, HttpContextBase ctx)
        {
            BriefingFilter filter = new BriefingFilter { TenantId = publication.OwnerTenantId, TopicId = publication.TopicId, Status = BriefingStatusEnum.Ready, IsDryRun = false, Active = true };
            ApplyPaging(ctx, filter);
            EnumerationResult<Briefing> page = await Server.Database.Briefings.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            List<CatalogIssueResponse> issues = new List<CatalogIssueResponse>();
            foreach (Briefing briefing in page.Objects)
            {
                CatalogIssueResponse? issue = await _Catalog.IssueAsync(publication, briefing.Id, ctx.Token).ConfigureAwait(false);
                if (issue != null) { issue.Markdown = null; issues.Add(issue); }
            }
            await SendJsonAsync(ctx, 200, EnumerationResult<CatalogIssueResponse>.Create(issues, filter, page.TotalRecords, page.TotalMs)).ConfigureAwait(false);
        }

        private async Task IssueAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await ReadableAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            User? reader = await Server.Database.Users.ReadAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false);
            CatalogIssueResponse? issue = await _Catalog.IssueAsync(publication, Param(ctx, "issueId")!, true, reader?.TimeZone ?? "UTC", ctx.Token).ConfigureAwait(false);
            if (issue == null) { await SendNotFoundAsync(ctx, "Issue").ConfigureAwait(false); return; }
            await SendJsonAsync(ctx, 200, issue).ConfigureAwait(false);
        }

        private async Task AudioAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await ReadableAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            EffectivePlan plan = await Server.Entitlements.GetEffectiveAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false);
            if (!EntitlementService.Allowed(plan, "catalog.audio.enabled"))
            { await SendErrorAsync(ctx, 403, "Forbidden", "Shared audio is not enabled for this account.").ConfigureAwait(false); return; }
            string issueId = Param(ctx, "issueId")!;
            if (await _Catalog.IssueAsync(publication, issueId, ctx.Token).ConfigureAwait(false) == null)
            { await SendNotFoundAsync(ctx, "Issue").ConfigureAwait(false); return; }
            MediaAsset? media = await _Catalog.AudioAsync(publication, issueId, ctx.Token).ConfigureAwait(false);
            Artifact? artifact = media?.ArtifactId == null ? null : await Server.Database.Artifacts.ReadAsync(publication.OwnerTenantId, media.ArtifactId, ctx.Token).ConfigureAwait(false);
            if (artifact == null || artifact.BriefingId != issueId) { await SendNotFoundAsync(ctx, "Audio").ConfigureAwait(false); return; }
            await SendArtifactAsync(ctx, Server.Artifacts, artifact, false).ConfigureAwait(false);
        }

        private async Task ProgressAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await ReadableAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            string issueId = Param(ctx, "issueId")!;
            if (await _Catalog.IssueAsync(publication, issueId, ctx.Token).ConfigureAwait(false) == null)
            { await SendNotFoundAsync(ctx, "Issue").ConfigureAwait(false); return; }
            BriefingProgress? progress = (await Server.Database.BriefingProgress.EnumerateAsync(new BriefingProgressFilter
            { TenantId = Context(ctx).TenantId, UserId = Context(ctx).UserId, BriefingId = issueId, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
            await SendJsonAsync(ctx, 200, progress ?? new BriefingProgress { TenantId = Context(ctx).TenantId!, UserId = Context(ctx).UserId!, BriefingId = issueId }).ConfigureAwait(false);
        }

        private async Task SaveProgressAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await ReadableAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            string issueId = Param(ctx, "issueId")!;
            if (await _Catalog.IssueAsync(publication, issueId, ctx.Token).ConfigureAwait(false) == null)
            { await SendNotFoundAsync(ctx, "Issue").ConfigureAwait(false); return; }
            BriefingProgressRequest? request = await ReadBodyAsync<BriefingProgressRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            if (!Double.IsFinite(request.PositionSeconds) || request.PositionSeconds < 0 || request.PositionSeconds > 86400
                || (request.DurationSeconds.HasValue && (!Double.IsFinite(request.DurationSeconds.Value) || request.DurationSeconds < 0 || request.DurationSeconds > 86400)))
            { await SendErrorAsync(ctx, 400, "ValidationFailed", "Playback times must be between zero and 86400 seconds.").ConfigureAwait(false); return; }
            BriefingProgress progress = await Server.Database.BriefingProgress.UpsertAsync(new BriefingProgress
            {
                TenantId = Context(ctx).TenantId!, UserId = Context(ctx).UserId!, BriefingId = issueId,
                PositionSeconds = request.PositionSeconds, DurationSeconds = request.DurationSeconds, Completed = request.Completed
            }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, progress).ConfigureAwait(false);
        }

        private async Task AdminListAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            CatalogPublicationFilter filter = new CatalogPublicationFilter();
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.CatalogPublications.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task<CatalogPublication?> AdminPublicationAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return null;
            CatalogPublication? publication = await Server.Database.CatalogPublications.ReadAsync(Param(ctx, "id")!, ctx.Token).ConfigureAwait(false);
            if (publication == null) await SendNotFoundAsync(ctx, "Publication").ConfigureAwait(false);
            return publication;
        }

        private async Task AdminReadAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await AdminPublicationAsync(ctx).ConfigureAwait(false);
            if (publication != null) await SendJsonAsync(ctx, 200, publication).ConfigureAwait(false);
        }

        private async Task<CatalogResearchResponse> ResearchSettingsAsync(CatalogPublication publication, HttpContextBase ctx)
        {
            Topic topic = await Server.Database.Topics.ReadAsync(publication.OwnerTenantId, publication.TopicId, ctx.Token).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("The canonical topic is unavailable.");
            Schedule? schedule = (await Server.Database.Schedules.EnumerateAsync(new ScheduleFilter
            { TenantId = topic.TenantId, OwnerType = ScheduleOwnerTypeEnum.Topic, OwnerId = topic.Id, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
            return new CatalogResearchResponse { Topic = topic, Schedule = schedule };
        }

        private async Task ResearchAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await AdminPublicationAsync(ctx).ConfigureAwait(false);
            if (publication != null) await SendJsonAsync(ctx, 200, await ResearchSettingsAsync(publication, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SaveResearchAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await AdminPublicationAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            CatalogResearchRequest? request = await ReadBodyAsync<CatalogResearchRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            CatalogResearchResponse settings = await ResearchSettingsAsync(publication, ctx).ConfigureAwait(false);
            Topic topic = settings.Topic;
            if (request.Topic != null)
            {
                TopicRoutes.Apply(topic, request.Topic);
                if (String.IsNullOrWhiteSpace(topic.Brief) && topic.Status == TopicStatusEnum.Active)
                { await SendErrorAsync(ctx, 400, "ValidationFailed", "Write the research scope before activating this topic.").ConfigureAwait(false); return; }
                if (!String.IsNullOrEmpty(topic.VoiceProfileId) && await Server.Database.VoiceProfiles.ReadAsync(topic.TenantId, topic.VoiceProfileId, ctx.Token).ConfigureAwait(false) == null)
                { await SendErrorAsync(ctx, 400, "ValidationFailed", "The voice must belong to the canonical workspace.").ConfigureAwait(false); return; }
                if (request.Topic.McpToolIds != null && request.Topic.McpToolIds.Count > 0)
                { await SendErrorAsync(ctx, 400, "ValidationFailed", "Configure system research with public search sources; private MCP tools are not accepted here.").ConfigureAwait(false); return; }
                await new MyMorningNews.Core.Briefings.HistoryContextService(Server.Database).ValidateAsync(topic, ctx.Token).ConfigureAwait(false);
            }
            Schedule? schedule = settings.Schedule;
            bool createSchedule = schedule == null;
            if (request.Schedule != null)
            {
                schedule ??= new Schedule { TenantId = topic.TenantId, UserId = topic.UserId, OwnerType = ScheduleOwnerTypeEnum.Topic, OwnerId = topic.Id, TimeZone = "UTC" };
                request.Schedule.OwnerId = null; request.Schedule.OwnerType = null;
                ScheduleRoutes.Apply(schedule, request.Schedule);
                List<string> errors = MyMorningNews.Core.Scheduling.ScheduleCalculator.Validate(schedule, Server.Settings.Scheduler.MinimumIntervalMinutes);
                if (errors.Count > 0) { await SendErrorAsync(ctx, 400, "ValidationFailed", String.Join(" ", errors)).ConfigureAwait(false); return; }
            }
            if (request.Topic != null)
            {
                settings.Topic = await Server.Database.Topics.UpdateAsync(topic, ctx.Token).ConfigureAwait(false);
                if (await Server.Database.TopicDimensions.CountAsync(new TopicDimensionFilter { TenantId = topic.TenantId, TopicId = topic.Id }, ctx.Token).ConfigureAwait(false) == 0)
                    await Server.Database.TopicDimensions.CreateAsync(new TopicDimension { TenantId = topic.TenantId, TopicId = topic.Id, Name = "Overview", Description = "Research the defined public scope.", Weight = DimensionWeightEnum.High }, ctx.Token).ConfigureAwait(false);
            }
            if (request.Schedule != null && schedule != null) settings.Schedule = await Server.Scheduling.SaveAsync(schedule, createSchedule, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.CatalogChanged, Context(ctx), 200, "Canonical research settings updated for " + publication.Id + ".", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, settings).ConfigureAwait(false);
        }

        private async Task RunAsync(HttpContextBase ctx)
        {
            CatalogPublication? publication = await AdminPublicationAsync(ctx).ConfigureAwait(false);
            if (publication == null) return;
            if (publication.Status != "Published") { await SendErrorAsync(ctx, 409, "Conflict", "Publish this topic before generating its shared issues.").ConfigureAwait(false); return; }
            CatalogResearchResponse settings = await ResearchSettingsAsync(publication, ctx).ConfigureAwait(false);
            await Server.Setup.RequireAsync(settings.Topic.TenantId, ModelTaskEnum.QueryPlanning, settings.Topic.Id, ctx.Token).ConfigureAwait(false);
            Run run = await Server.Scheduling.RunNowAsync(settings.Topic, settings.Topic.UserId, false, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, run).ConfigureAwait(false);
        }

        private static string? Validate(CatalogPublicationRequest request)
        {
            if (String.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200) return "Title must be 1 to 200 characters.";
            if (request.Description?.Length > 4000) return "Description must be at most 4000 characters.";
            if (String.IsNullOrWhiteSpace(request.Category) || request.Category.Length > 100) return "Category must be 1 to 100 characters.";
            if (String.IsNullOrWhiteSpace(request.Language) || request.Language.Length > 16) return "Language must be 1 to 16 characters.";
            if (!new[] { "Draft", "Published", "Retired", "Retracted" }.Contains(request.Status)) return "Unknown publication status.";
            return null;
        }

        private static void Apply(CatalogPublication publication, CatalogPublicationRequest request)
        {
            publication.Title = request.Title.Trim(); publication.Description = request.Description?.Trim();
            publication.Category = request.Category.Trim(); publication.Language = request.Language.Trim();
            publication.FreeEligible = request.FreeEligible; publication.SortOrder = request.SortOrder; publication.Status = request.Status;
            if (request.Status == "Published") publication.PublishedUtc ??= DateTime.UtcNow;
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            CatalogPublicationRequest? request = await ReadBodyAsync<CatalogPublicationRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            string? error = Validate(request);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            User owner = await Server.Database.CatalogPublications.EnsureOwnerAsync(ctx.Token).ConfigureAwait(false);
            await Server.TenantDefaults.EnsureAsync(owner.TenantId, ctx.Token).ConfigureAwait(false);
            Topic topic = await Server.Refinement.CreateTopicAsync(owner.TenantId, owner.Id, request.Title, request.Description, false, ctx.Token).ConfigureAwait(false);
            CatalogPublication publication = new CatalogPublication { TopicId = topic.Id, OwnerTenantId = owner.TenantId };
            Apply(publication, request);
            publication = await Server.Database.CatalogPublications.CreateAsync(publication, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.CatalogChanged, Context(ctx), 201, "System publication " + publication.Id + " created.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, publication).ConfigureAwait(false);
        }

        private async Task PromoteAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            CatalogPublicationRequest? request = await ReadBodyAsync<CatalogPublicationRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            string? error = Validate(request);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            string tenantId = Context(ctx).TenantId ?? String.Empty;
            Topic? source = String.IsNullOrEmpty(tenantId) ? null : await Server.Database.Topics.ReadAsync(tenantId, Param(ctx, "topicId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (source == null || source.UserId != Context(ctx).UserId) { await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false); return; }
            CatalogPublication publication = new CatalogPublication { TopicId = source.Id, OwnerTenantId = tenantId };
            Apply(publication, request);
            try
            {
                publication = await new CatalogPromotionService(Server.Database, Server.Refinement, Server.TenantDefaults, Server.Scheduling).PromoteAsync(source, publication, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", ex.Message).ConfigureAwait(false);
                return;
            }

            await Server.Audit.RecordAsync(AuditEventTypeEnum.CatalogChanged, Context(ctx), 201, "System publication " + publication.Id + " created from topic " + source.Id + ".", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, publication).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            CatalogPublicationRequest? request = await ReadBodyAsync<CatalogPublicationRequest>(ctx).ConfigureAwait(false);
            if (request == null) return;
            string? error = Validate(request);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            CatalogPublication? publication = await Server.Database.CatalogPublications.ReadAsync(Param(ctx, "id")!, ctx.Token).ConfigureAwait(false);
            if (publication == null) { await SendNotFoundAsync(ctx, "Publication").ConfigureAwait(false); return; }
            if (request.Revision != publication.Revision) { await SendErrorAsync(ctx, 409, "Conflict", "Reload the publication before saving your changes.").ConfigureAwait(false); return; }
            Apply(publication, request);
            publication.Revision++;
            try { publication = await Server.Database.CatalogPublications.UpdateAsync(publication, ctx.Token).ConfigureAwait(false); }
            catch (KeyNotFoundException) { await SendErrorAsync(ctx, 409, "Conflict", "The publication changed. Reload before saving.").ConfigureAwait(false); return; }
            if (publication.Status == "Retired" || publication.Status == "Retracted")
            {
                Topic? canonical = await Server.Database.Topics.ReadAsync(publication.OwnerTenantId, publication.TopicId, ctx.Token).ConfigureAwait(false);
                if (canonical != null)
                {
                    canonical.Status = TopicStatusEnum.Paused;
                    await Server.Database.Topics.UpdateAsync(canonical, ctx.Token).ConfigureAwait(false);
                }
            }
            await Server.Audit.RecordAsync(AuditEventTypeEnum.CatalogChanged, Context(ctx), 200, "System publication " + publication.Id + " updated to " + publication.Status + ".", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, publication).ConfigureAwait(false);
        }
    }
}
