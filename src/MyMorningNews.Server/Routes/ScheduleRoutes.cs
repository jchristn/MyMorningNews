namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Scheduling;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Schedules for topics (and, from Phase 7, editions) with a live preview of upcoming runs (plan section 14).
    /// </summary>
    public class ScheduleRoutes : RouteBase
    {
        private const string B = "/v1.0/api/schedules";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public ScheduleRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B + "/preview", PreviewAsync, null, openApiMetadata: Doc("Validate a schedule and preview its next runs", "Schedules").WithRequestBody(Body<ScheduleUpsertRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, ListAsync, null, openApiMetadata: Paged(Doc("List schedules", "Schedules").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("ownerType", typeof(ScheduleOwnerTypeEnum))).WithParameter(QueryParam("ownerId", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List schedules", "Schedules").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("ownerType", typeof(ScheduleOwnerTypeEnum))).WithParameter(QueryParam("ownerId", typeof(string)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B, CreateAsync, null, openApiMetadata: Doc("Create a schedule", "Schedules").WithRequestBody(Body<ScheduleUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a schedule", "Schedules"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update a schedule", "Schedules").WithRequestBody(Body<ScheduleUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a schedule", "Schedules"));
        }

        private async Task PreviewAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ScheduleRead).ConfigureAwait(false)) return;
            ScheduleUpsertRequest? req = await ReadBodyAsync<ScheduleUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Schedule draft = new Schedule { CreatedUtc = DateTime.UtcNow, TimeZone = "UTC" };
            Apply(draft, req);
            List<string> errors = ScheduleCalculator.Validate(draft, Server.Settings.Scheduler.MinimumIntervalMinutes);
            SchedulePreviewResponse preview = new SchedulePreviewResponse { Valid = errors.Count == 0, Errors = errors };
            if (preview.Valid)
            {
                preview.NextRunsUtc = ScheduleCalculator.NextOccurrences(draft, DateTime.UtcNow, Math.Clamp(req.PreviewCount ?? 5, 1, 20));
                TimeZoneInfo tz = TimeZoneHelper.TryFind(draft.TimeZone) ?? TimeZoneInfo.Utc;
                preview.NextRunsLocal = preview.NextRunsUtc.Select(u => new DateTimeOffset(u).ToOffset(tz.GetUtcOffset(u)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)).ToList();
            }

            await SendJsonAsync(ctx, 200, preview).ConfigureAwait(false);
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ScheduleRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            ScheduleFilter filter = new ScheduleFilter
            {
                TenantId = tenantId,
                OwnerType = QueryEnum<ScheduleOwnerTypeEnum>(ctx, "ownerType"),
                OwnerId = Query(ctx, "ownerId"),
                UserId = ListScope(ctx)
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Schedules.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ScheduleWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            ScheduleUpsertRequest? req = await ReadBodyAsync<ScheduleUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.OwnerId) || !req.IntervalType.HasValue)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "ownerId and intervalType are required.").ConfigureAwait(false);
                return;
            }

            ScheduleOwnerTypeEnum ownerType = req.OwnerType ?? ScheduleOwnerTypeEnum.Topic;
            string? ownerUser = await OwnerUserAsync(tenantId, ownerType, req.OwnerId, ctx).ConfigureAwait(false);
            if (ownerUser == null) return;
            Schedule s = new Schedule { TenantId = tenantId, OwnerType = ownerType, OwnerId = req.OwnerId, UserId = ownerUser, TimeZone = "UTC" };
            Apply(s, req);
            s = await Server.Scheduling.SaveAsync(s, true, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, s).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            Schedule? s = await OwnedAsync(ctx, RequestTypeEnum.ScheduleRead).ConfigureAwait(false);
            if (s == null) return;
            await SendJsonAsync(ctx, 200, s).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            Schedule? s = await OwnedAsync(ctx, RequestTypeEnum.ScheduleWrite).ConfigureAwait(false);
            if (s == null) return;
            ScheduleUpsertRequest? req = await ReadBodyAsync<ScheduleUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            req.OwnerId = null;
            req.OwnerType = null;
            Apply(s, req);
            s = await Server.Scheduling.SaveAsync(s, false, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, s).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            Schedule? s = await OwnedAsync(ctx, RequestTypeEnum.ScheduleWrite).ConfigureAwait(false);
            if (s == null) return;
            await Server.Database.Schedules.DeleteAsync(s.TenantId, s.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task<Schedule?> OwnedAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Schedule? s = await Server.Database.Schedules.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (s == null || !IsOwnerOrAdmin(ctx, s.UserId))
            {
                await SendNotFoundAsync(ctx, "Schedule").ConfigureAwait(false);
                return null;
            }

            return s;
        }

        private async Task<string?> OwnerUserAsync(string tenantId, ScheduleOwnerTypeEnum ownerType, string ownerId, HttpContextBase ctx)
        {
            if (ownerType == ScheduleOwnerTypeEnum.Edition)
            {
                Edition? edition = await Server.Database.Editions.ReadAsync(tenantId, ownerId, ctx.Token).ConfigureAwait(false);
                if (edition == null || !IsOwnerOrAdmin(ctx, edition.UserId))
                {
                    await SendNotFoundAsync(ctx, "Edition").ConfigureAwait(false);
                    return null;
                }

                return edition.UserId;
            }

            if (ownerType != ScheduleOwnerTypeEnum.Topic)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Schedules belong to a topic or an edition; one-time requests schedule themselves.").ConfigureAwait(false);
                return null;
            }

            Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, ownerId, ctx.Token).ConfigureAwait(false);
            if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId))
            {
                await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false);
                return null;
            }

            return topic.UserId;
        }

        internal static void Apply(Schedule s, ScheduleUpsertRequest req)
        {
            if (req.Name != null) s.Name = req.Name.Trim().Length == 0 ? null : req.Name.Trim();
            if (req.IntervalType.HasValue) s.IntervalType = req.IntervalType.Value;
            if (req.IntervalValue.HasValue) s.IntervalValue = req.IntervalValue.Value;
            if (req.MinuteOffset.HasValue) s.MinuteOffset = req.MinuteOffset.Value;
            if (req.DaysOfWeekMask.HasValue) s.DaysOfWeekMask = req.DaysOfWeekMask.Value;
            if (req.DaysOfMonth != null) s.DaysOfMonth = req.DaysOfMonth;
            if (req.TimesOfDay != null) s.TimesOfDay = req.TimesOfDay;
            if (req.CronExpression != null) s.CronExpression = req.CronExpression.Trim().Length == 0 ? null : req.CronExpression.Trim();
            if (!String.IsNullOrWhiteSpace(req.TimeZone)) s.TimeZone = req.TimeZone.Trim();
            if (req.StartUtc.HasValue) s.StartUtc = DateTime.SpecifyKind(req.StartUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (req.EndUtc.HasValue) s.EndUtc = DateTime.SpecifyKind(req.EndUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (req.RunOnceUtc.HasValue) s.RunOnceUtc = DateTime.SpecifyKind(req.RunOnceUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (req.Enabled.HasValue) s.Enabled = req.Enabled.Value;
            if (req.MissedRunPolicy.HasValue) s.MissedRunPolicy = req.MissedRunPolicy.Value;
        }
    }
}
