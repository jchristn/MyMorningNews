namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tenant control-plane routes. Global administrators manage every tenant; others can read only their own.
    /// </summary>
    public class TenantRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public TenantRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/tenants", ListAsync, null, openApiMetadata: Paged(Doc("List tenants", "Tenants").WithParameter(QueryParam("active", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/tenants" + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List tenants", "Tenants").WithParameter(QueryParam("active", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/tenants", CreateAsync, null, openApiMetadata: Doc("Create a tenant", "Tenants").WithRequestBody(Body<TenantUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}", ReadAsync, null, openApiMetadata: Doc("Read a tenant", "Tenants"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}", UpdateAsync, null, openApiMetadata: Doc("Update a tenant", "Tenants").WithRequestBody(Body<TenantUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}", DeleteAsync, null, openApiMetadata: Doc("Delete a tenant and all of its data", "Tenants"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TenantRead).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            TenantFilter filter = new TenantFilter();
            ApplyPaging(ctx, filter);
            filter.Active = QueryBool(ctx, "active");
            if (rc.IsAdmin)
            {
                await SendJsonAsync(ctx, 200, await Server.Database.Tenants.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            Tenant? own = rc.TenantId != null ? await Server.Database.Tenants.ReadAsync(rc.TenantId, ctx.Token).ConfigureAwait(false) : null;
            List<Tenant> items = own != null ? new List<Tenant> { own } : new List<Tenant>();
            await SendJsonAsync(ctx, 200, EnumerationResult<Tenant>.Create(items, filter, items.Count, 0)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TenantWrite).ConfigureAwait(false)) return;
            if (!Context(ctx).IsAdmin)
            {
                await SendErrorAsync(ctx, 403, "Forbidden", "Only global administrators can create tenants.").ConfigureAwait(false);
                return;
            }

            TenantUpsertRequest? req = await ReadBodyAsync<TenantUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Name is required.", new Dictionary<string, string> { { "name", "Required." } }).ConfigureAwait(false);
                return;
            }

            string? tzError = TimeZoneHelper.Validate(req.TimeZone);
            if (tzError != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", tzError, new Dictionary<string, string> { { "timeZone", tzError } }).ConfigureAwait(false);
                return;
            }

            Tenant tenant = new Tenant { Name = req.Name.Trim(), Description = req.Description, Region = req.Region, TimeZone = req.TimeZone ?? "UTC", Active = req.Active ?? true };
            tenant = await Server.Database.Tenants.CreateAsync(tenant, ctx.Token).ConfigureAwait(false);
            await Server.TenantDefaults.EnsureAsync(tenant.Id, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.TenantChanged, Context(ctx), 201, "Tenant " + tenant.Id + " created.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, tenant).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TenantRead, tenantId).ConfigureAwait(false)) return;
            Tenant? tenant = await Server.Database.Tenants.ReadAsync(tenantId, ctx.Token).ConfigureAwait(false);
            if (tenant == null)
            {
                await SendNotFoundAsync(ctx, "Tenant").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, tenant).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TenantWrite, tenantId).ConfigureAwait(false)) return;
            TenantUpsertRequest? req = await ReadBodyAsync<TenantUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Tenant? tenant = await Server.Database.Tenants.ReadAsync(tenantId, ctx.Token).ConfigureAwait(false);
            if (tenant == null)
            {
                await SendNotFoundAsync(ctx, "Tenant").ConfigureAwait(false);
                return;
            }

            string? tzError = TimeZoneHelper.Validate(req.TimeZone);
            if (tzError != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", tzError, new Dictionary<string, string> { { "timeZone", tzError } }).ConfigureAwait(false);
                return;
            }

            if (req.Active == false && tenant.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "A protected tenant cannot be deactivated.").ConfigureAwait(false);
                return;
            }

            if (!String.IsNullOrWhiteSpace(req.Name)) tenant.Name = req.Name.Trim();
            if (req.Description != null) tenant.Description = req.Description;
            if (req.Region != null) tenant.Region = req.Region;
            if (req.TimeZone != null) tenant.TimeZone = req.TimeZone;
            if (req.Active.HasValue) tenant.Active = req.Active.Value;
            tenant = await Server.Database.Tenants.UpdateAsync(tenant, ctx.Token).ConfigureAwait(false);
            Server.Authorization.Invalidate();
            await Server.Audit.RecordAsync(AuditEventTypeEnum.TenantChanged, Context(ctx), 200, "Tenant " + tenant.Id + " updated.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, tenant).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TenantDelete, tenantId).ConfigureAwait(false)) return;
            if (!Context(ctx).IsAdmin)
            {
                await SendErrorAsync(ctx, 403, "Forbidden", "Only global administrators can delete tenants.").ConfigureAwait(false);
                return;
            }

            Tenant? tenant = await Server.Database.Tenants.ReadAsync(tenantId, ctx.Token).ConfigureAwait(false);
            if (tenant == null)
            {
                await SendNotFoundAsync(ctx, "Tenant").ConfigureAwait(false);
                return;
            }

            if (tenant.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "A protected tenant cannot be deleted.").ConfigureAwait(false);
                return;
            }

            await Server.Database.Tenants.DeleteAsync(tenantId, ctx.Token).ConfigureAwait(false);
            Server.Authorization.Invalidate();
            await Server.Audit.RecordAsync(AuditEventTypeEnum.TenantChanged, Context(ctx), 204, "Tenant " + tenantId + " deleted.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }
    }
}
