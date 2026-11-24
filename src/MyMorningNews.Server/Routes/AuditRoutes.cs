namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Audit record routes, tenant scoped. Global administrators may widen scope with ?tenantId=.
    /// </summary>
    public class AuditRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public AuditRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/audit", ListAsync, null, openApiMetadata: Paged(Doc("List audit records", "Audit").WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("principalId", typeof(string))).WithParameter(QueryParam("authorizationResult", typeof(string))).WithParameter(QueryParam("eventType", typeof(AuditEventTypeEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/audit" + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List audit records", "Audit").WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("principalId", typeof(string))).WithParameter(QueryParam("authorizationResult", typeof(string))).WithParameter(QueryParam("eventType", typeof(AuditEventTypeEnum)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.DELETE, "/v1.0/api/audit", DeleteManyAsync, null, openApiMetadata: Doc("Delete audit records older than a cutoff", "Audit", "Requires toUtc.").WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("toUtc", typeof(DateTime))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/audit/{id}", ReadAsync, null, openApiMetadata: Doc("Read an audit record", "Audit"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/audit/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an audit record", "Audit"));
        }

        private string? Scope(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            string? requested = Query(ctx, "tenantId");
            if (rc.IsAdmin) return requested;
            return rc.TenantId;
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AuditRead).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            string? tenant = Scope(ctx);
            AuditRecordFilter filter = new AuditRecordFilter
            {
                TenantId = tenant,
                IncludeGlobal = rc.IsAdmin && tenant == null,
                EventType = QueryEnum<AuditEventTypeEnum>(ctx, "eventType"),
                UserId = Query(ctx, "userId"),
                PrincipalId = Query(ctx, "principalId"),
                AuthorizationResult = Query(ctx, "authorizationResult")
            };
            ApplyPaging(ctx, filter);
            if (rc.IsAdmin && tenant == null)
            {
                EnumerationResult<AuditRecord> all = await ListAllTenantsAsync(filter, ctx).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, all).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, await Server.Database.AuditRecords.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task<EnumerationResult<AuditRecord>> ListAllTenantsAsync(AuditRecordFilter filter, HttpContextBase ctx)
        {
            filter.TenantId = null;
            filter.AllTenants = true;
            return await Server.Database.AuditRecords.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AuditRead).ConfigureAwait(false)) return;
            AuditRecord? record = await Server.Database.AuditRecords.ReadByIdAsync(Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            RequestContext rc = Context(ctx);
            if (record == null || (!rc.IsAdmin && record.TenantId != rc.TenantId))
            {
                await SendNotFoundAsync(ctx, "Audit record").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, record).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AuditDelete).ConfigureAwait(false)) return;
            AuditRecord? record = await Server.Database.AuditRecords.ReadByIdAsync(Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            RequestContext rc = Context(ctx);
            if (record == null || (!rc.IsAdmin && record.TenantId != rc.TenantId))
            {
                await SendNotFoundAsync(ctx, "Audit record").ConfigureAwait(false);
                return;
            }

            await Server.Database.AuditRecords.DeleteByIdAsync(record.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task DeleteManyAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.AuditDelete).ConfigureAwait(false)) return;
            DateTime? cutoff = QueryDate(ctx, "toUtc");
            if (!cutoff.HasValue)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "toUtc is required; records created before it are deleted.").ConfigureAwait(false);
                return;
            }

            int deleted = await Server.Database.AuditRecords.DeleteOlderThanAsync(Scope(ctx), cutoff.Value, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new DeleteManyResponse { DeletedCount = deleted }).ConfigureAwait(false);
        }
    }
}
