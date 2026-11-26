namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Editions (plan section 21.3): recurring briefings across topics with their own schedule, template, and delivery.
    /// Schedules are managed through /v1.0/api/schedules with ownerType Edition.
    /// </summary>
    public class EditionRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public EditionRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/editions";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List my editions", "Editions").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List my editions", "Editions").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b, CreateAsync, null, openApiMetadata: Doc("Create an edition", "Editions").WithRequestBody(Body<EditionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read an edition with its topics and schedules", "Editions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update an edition", "Editions", "Only the fields sent are changed. historyMode other than Off compares each composed edition with its own earlier issues (historyCount, historySinceUtc, historyBriefingId, historyTokenBudget as for topics); Off keeps the topics' own research comparisons.").WithRequestBody(Body<EditionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an edition (its briefings stay)", "Editions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}/topics", TopicsAsync, null, openApiMetadata: Doc("Set an edition's topics in order", "Editions",
                "An empty list means all of the owner's active recurring topics.").WithRequestBody(Body<EditionTopicsRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/publications", PublicationsAsync, null, openApiMetadata: Paged(Doc("List the system briefings an edition includes", "Editions",
                "In edition order. Each entry says whether it can be read now; unavailable publications are skipped when the edition is composed.")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/publications/enumerate", ctx => EnumerateAsync(ctx, PublicationsAsync), null, openApiMetadata: Paged(Doc("List the system briefings an edition includes", "Editions",
                "In edition order. Each entry says whether it can be read now; unavailable publications are skipped when the edition is composed.")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}/publications", SetPublicationsAsync, null, openApiMetadata: Doc("Set the system briefings an edition includes", "Editions",
                "Ordered publication IDs you are subscribed to, at most 20. The edition summarizes their published issues in its period and links to each; it never starts system research. Requires a plan with custom research (402 CapReached otherwise). An empty list removes them.").WithRequestBody(Body<EditionPublicationsRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/history/preview", HistoryPreviewAsync, null, openApiMetadata: Doc("Preview an edition's comparison baseline", "Editions",
                "Shows which earlier issues of this edition the next composition would compare with. The optional body previews unsaved settings; nothing is saved.").WithRequestBody(Body<HistoryPreviewRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/compose", ComposeAsync, null, openApiMetadata: Doc("Compose an edition now", "Editions",
                "Queues the briefing (and fresh research when enabled) and returns 202 with the queued briefing."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/preview", PreviewAsync, null, openApiMetadata: Doc("Preview an edition from the latest data without saving", "Editions",
                "Optional body {templateContent} previews unsaved template Markdown (400 TemplateInvalid when it has errors).").WithRequestBody(Body<EditionPreviewRequest>()));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.EditionRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            EditionFilter filter = new EditionFilter { TenantId = tenantId, UserId = ListScope(ctx) };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Editions.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.EditionWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            EditionUpsertRequest? req = await ReadBodyAsync<EditionUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "name is required.").ConfigureAwait(false);
                return;
            }

            Edition e = new Edition { TenantId = tenantId, UserId = Context(ctx).UserId ?? String.Empty };
            if (!await ApplyAsync(ctx, e, req).ConfigureAwait(false)) return;
            e = await Server.Database.Editions.CreateAsync(e, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await DescribeAsync(e, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionRead).ConfigureAwait(false);
            if (e == null) return;
            await SendJsonAsync(ctx, 200, await DescribeAsync(e, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionWrite).ConfigureAwait(false);
            if (e == null) return;
            EditionUpsertRequest? req = await ReadBodyAsync<EditionUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (!await ApplyAsync(ctx, e, req).ConfigureAwait(false)) return;
            e = await Server.Database.Editions.UpdateAsync(e, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribeAsync(e, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionWrite).ConfigureAwait(false);
            if (e == null) return;
            await Server.Briefings.DeleteEditionAsync(e, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task TopicsAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionWrite).ConfigureAwait(false);
            if (e == null) return;
            EditionTopicsRequest? req = await ReadBodyAsync<EditionTopicsRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await Server.Briefings.SetTopicsAsync(e, req.TopicIds ?? new List<string>(), ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribeAsync(e, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ComposeAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.BriefingWrite).ConfigureAwait(false);
            if (e == null) return;
            Briefing? active = (await Server.Database.Briefings.EnumerateAsync(new BriefingFilter { TenantId = e.TenantId, EditionId = e.Id, PageSize = 20 }, ctx.Token).ConfigureAwait(false)).Objects
                .FirstOrDefault(x => x.Status == BriefingStatusEnum.Queued || x.Status == BriefingStatusEnum.Composing);
            if (active != null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "This edition already has a briefing on the way.", new Dictionary<string, string> { { "briefingId", active.Id }, { "status", active.Status.ToString() } }).ConfigureAwait(false);
                return;
            }

            Briefing queued = await Server.Briefings.QueueEditionAsync(e, null, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, queued).ConfigureAwait(false);
        }

        private async Task PreviewAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false);
            if (e == null) return;
            EditionPreviewRequest req = (await ReadOptionalBodyAsync<EditionPreviewRequest>(ctx).ConfigureAwait(false)) ?? new EditionPreviewRequest();
            Briefing preview = await Server.Briefings.PreviewEditionAsync(e, req.TemplateContent, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, preview).ConfigureAwait(false);
        }

        private async Task PublicationsAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionRead).ConfigureAwait(false);
            if (e == null) return;
            EnumerationQuery query = new EnumerationQuery();
            ApplyPaging(ctx, query);
            List<EditionPublicationResponse> all = await DescribePublicationsAsync(e, ctx).ConfigureAwait(false);
            long offset = (long)(query.PageNumber - 1) * query.PageSize;
            List<EditionPublicationResponse> items = offset >= all.Count ? new List<EditionPublicationResponse>() : all.Skip((int)offset).Take(query.PageSize).ToList();
            await SendJsonAsync(ctx, 200, EnumerationResult<EditionPublicationResponse>.Create(items, query, all.Count, 0)).ConfigureAwait(false);
        }

        private async Task SetPublicationsAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionWrite).ConfigureAwait(false);
            if (e == null) return;
            EditionPublicationsRequest? req = await ReadBodyAsync<EditionPublicationsRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            List<string> ids = (req.PublicationIds ?? new List<string>()).Select(x => (x ?? String.Empty).Trim()).ToList();
            if (ids.Count > 20 || ids.Any(x => x.Length == 0) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "publicationIds must be at most 20 distinct, non-blank IDs.").ConfigureAwait(false);
                return;
            }

            if (ids.Count > 0) await Server.Entitlements.CheckResearchAsync(e.TenantId, e.UserId, false, ctx.Token).ConfigureAwait(false);
            MyMorningNews.Core.Catalog.CatalogService catalog = new MyMorningNews.Core.Catalog.CatalogService(Server.Database, Server.Entitlements);
            foreach (string id in ids)
            {
                if (await catalog.ReadableAsync(e.TenantId, e.UserId, id, ctx.Token).ConfigureAwait(false) == null)
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", "Publication " + id + " is not one of your active system briefing subscriptions.").ConfigureAwait(false);
                    return;
                }
            }

            await Server.Database.EditionPublications.ReplaceAsync(e.TenantId, e.Id, ids, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribeAsync(e, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task HistoryPreviewAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.EditionRead).ConfigureAwait(false);
            if (e == null) return;
            HistoryPreviewRequest? req = await ReadOptionalBodyAsync<HistoryPreviewRequest>(ctx).ConfigureAwait(false);
            HistoryPreviewResponse preview = await new MyMorningNews.Core.Briefings.HistoryContextService(Server.Database).PreviewAsync(e, req, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, preview).ConfigureAwait(false);
        }

        private async Task<List<EditionPublicationResponse>> DescribePublicationsAsync(Edition e, HttpContextBase ctx)
        {
            List<EditionPublication> links = (await Server.Database.EditionPublications.EnumerateAsync(new EditionPublicationFilter { TenantId = e.TenantId, EditionId = e.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Objects;
            List<EditionPublicationResponse> result = new List<EditionPublicationResponse>();
            if (links.Count == 0) return result;
            List<CatalogPublication> readable = await Server.Briefings.ReadableEditionPublicationsAsync(e, ctx.Token).ConfigureAwait(false);
            foreach (EditionPublication link in links)
            {
                CatalogPublication? pub = readable.FirstOrDefault(p => p.Id == link.PublicationId);
                if (pub == null)
                {
                    CatalogPublication? raw = await Server.Database.CatalogPublications.ReadAsync(link.PublicationId, ctx.Token).ConfigureAwait(false);
                    result.Add(new EditionPublicationResponse { PublicationId = link.PublicationId, Title = raw != null && raw.Active && (raw.Status == "Published" || raw.Status == "Retired") ? raw.Title : String.Empty, SortOrder = link.SortOrder, Available = false });
                }
                else
                {
                    result.Add(new EditionPublicationResponse { PublicationId = pub.Id, Title = pub.Title, SortOrder = link.SortOrder, Available = true });
                }
            }

            return result;
        }

        private async Task<bool> ApplyAsync(HttpContextBase ctx, Edition e, EditionUpsertRequest req)
        {
            if (req.Name != null)
            {
                string name = req.Name.Trim();
                if (name.Length == 0 || name.Length > 128)
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", "name must be 1 to 128 characters.").ConfigureAwait(false);
                    return false;
                }

                List<Edition> mine = (await Server.Database.Editions.EnumerateAsync(new EditionFilter { TenantId = e.TenantId, UserId = e.UserId, PageSize = 1000 }, ctx.Token).ConfigureAwait(false)).Objects;
                if (mine.Any(x => x.Id != e.Id && String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    await SendErrorAsync(ctx, 409, "Conflict", "You already have an edition named '" + name + "'.").ConfigureAwait(false);
                    return false;
                }

                e.Name = name;
            }

            if (req.Description != null) e.Description = req.Description.Trim().Length == 0 ? null : req.Description.Trim();
            if (req.BriefingTemplateId != null)
            {
                if (req.BriefingTemplateId.Length == 0)
                {
                    e.BriefingTemplateId = null;
                }
                else
                {
                    BriefingTemplate? t = await Server.Database.BriefingTemplates.ReadAsync(e.TenantId, req.BriefingTemplateId, ctx.Token).ConfigureAwait(false);
                    if (t == null || (t.UserId != null && t.UserId != e.UserId))
                    {
                        await SendErrorAsync(ctx, 400, "ValidationFailed", "The briefing template was not found.").ConfigureAwait(false);
                        return false;
                    }

                    e.BriefingTemplateId = t.Id;
                }
            }

            if (req.AudioEnabled.HasValue) e.AudioEnabled = req.AudioEnabled.Value;
            if (req.PushEnabled.HasValue) e.PushEnabled = req.PushEnabled.Value;
            if (req.EmailEnabled.HasValue) e.EmailEnabled = req.EmailEnabled.Value;
            if (req.Enabled.HasValue) e.Enabled = req.Enabled.Value;
            if (req.FreshResearchBeforeCompose.HasValue) e.FreshResearchBeforeCompose = req.FreshResearchBeforeCompose.Value;
            if (req.FreshResearchLeadMinutes.HasValue) e.FreshResearchLeadMinutes = req.FreshResearchLeadMinutes.Value;
            if (req.LookbackMode.HasValue) e.LookbackMode = req.LookbackMode.Value;
            if (req.LookbackHours.HasValue) e.LookbackHours = req.LookbackHours.Value;
            if (req.HistoryMode.HasValue) e.HistoryMode = req.HistoryMode.Value;
            if (req.HistoryCount.HasValue) e.HistoryCount = req.HistoryCount.Value;
            if (req.HistorySinceUtc.HasValue) e.HistorySinceUtc = req.HistorySinceUtc;
            if (req.HistoryBriefingId != null) e.HistoryBriefingId = req.HistoryBriefingId.Length == 0 ? null : req.HistoryBriefingId;
            if (req.HistoryTokenBudget.HasValue) e.HistoryTokenBudget = req.HistoryTokenBudget.Value;
            await new MyMorningNews.Core.Briefings.HistoryContextService(Server.Database).ValidateAsync(e, ctx.Token).ConfigureAwait(false);
            return true;
        }

        private async Task<EditionResponse> DescribeAsync(Edition e, HttpContextBase ctx)
        {
            List<EditionTopic> topics = (await Server.Database.EditionTopics.EnumerateAsync(new EditionTopicFilter { TenantId = e.TenantId, EditionId = e.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Objects
                .OrderBy(t => t.SortOrder).ToList();
            List<Schedule> schedules = (await Server.Database.Schedules.EnumerateAsync(new ScheduleFilter { TenantId = e.TenantId, OwnerType = ScheduleOwnerTypeEnum.Edition, OwnerId = e.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Objects;
            List<string> covered = (await Server.Briefings.EditionTopicsAsync(e, ctx.Token).ConfigureAwait(false)).Select(t => t.Id).ToList();
            return new EditionResponse { Edition = e, Topics = topics, Schedules = schedules, CoveredTopicIds = covered, Publications = await DescribePublicationsAsync(e, ctx).ConfigureAwait(false) };
        }

        private async Task<Edition?> OwnedAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Edition? e = await Server.Database.Editions.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (e == null || !IsOwnerOrAdmin(ctx, e.UserId))
            {
                await SendNotFoundAsync(ctx, "Edition").ConfigureAwait(false);
                return null;
            }

            return e;
        }
    }
}
