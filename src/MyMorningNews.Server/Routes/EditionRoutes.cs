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
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List my editions", "Editions")));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b, CreateAsync, null, openApiMetadata: Doc("Create an edition", "Editions").WithRequestBody(Body<EditionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read an edition with its topics and schedules", "Editions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update an edition", "Editions").WithRequestBody(Body<EditionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an edition (its briefings stay)", "Editions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}/topics", TopicsAsync, null, openApiMetadata: Doc("Set an edition's topics in order", "Editions",
                "An empty list means all of the owner's active recurring topics.").WithRequestBody(Body<EditionTopicsRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/compose", ComposeAsync, null, openApiMetadata: Doc("Compose an edition now", "Editions",
                "Queues the briefing (and fresh research when enabled) and returns 202 with the queued briefing."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/preview", PreviewAsync, null, openApiMetadata: Doc("Preview an edition from the latest data without saving", "Editions"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.EditionRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            EditionFilter filter = new EditionFilter { TenantId = tenantId, UserId = rc.IsAdmin || rc.IsTenantAdmin ? Query(ctx, "userId") : rc.UserId };
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
            Briefing queued = await Server.Briefings.QueueEditionAsync(e, null, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, queued).ConfigureAwait(false);
        }

        private async Task PreviewAsync(HttpContextBase ctx)
        {
            Edition? e = await OwnedAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false);
            if (e == null) return;
            Briefing preview = await Server.Briefings.PreviewEditionAsync(e, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, preview).ConfigureAwait(false);
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

                List<Edition> mine = (await Server.Database.Editions.EnumerateAsync(new EditionFilter { TenantId = e.TenantId, UserId = e.UserId, PageSize = 1000 }, ctx.Token).ConfigureAwait(false)).Items;
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
            if (req.EmailEnabled.HasValue) e.EmailEnabled = req.EmailEnabled.Value;
            if (req.Enabled.HasValue) e.Enabled = req.Enabled.Value;
            if (req.FreshResearchBeforeCompose.HasValue) e.FreshResearchBeforeCompose = req.FreshResearchBeforeCompose.Value;
            if (req.FreshResearchLeadMinutes.HasValue) e.FreshResearchLeadMinutes = req.FreshResearchLeadMinutes.Value;
            if (req.LookbackMode.HasValue) e.LookbackMode = req.LookbackMode.Value;
            if (req.LookbackHours.HasValue) e.LookbackHours = req.LookbackHours.Value;
            return true;
        }

        private async Task<EditionResponse> DescribeAsync(Edition e, HttpContextBase ctx)
        {
            List<EditionTopic> topics = (await Server.Database.EditionTopics.EnumerateAsync(new EditionTopicFilter { TenantId = e.TenantId, EditionId = e.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Items
                .OrderBy(t => t.SortOrder).ToList();
            List<Schedule> schedules = (await Server.Database.Schedules.EnumerateAsync(new ScheduleFilter { TenantId = e.TenantId, OwnerType = ScheduleOwnerTypeEnum.Edition, OwnerId = e.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Items;
            List<string> covered = (await Server.Briefings.EditionTopicsAsync(e, ctx.Token).ConfigureAwait(false)).Select(t => t.Id).ToList();
            return new EditionResponse { Edition = e, Topics = topics, Schedules = schedules, CoveredTopicIds = covered };
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
