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
    using MyMorningNews.Core.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// API credential routes (access key and secret). The raw secret is returned only once, at creation.
    /// </summary>
    public class CredentialRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public CredentialRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/credentials", ListAsync, null, openApiMetadata: Paged(Doc("List credentials", "Credentials").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("active", typeof(bool)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/credentials", CreateAsync, null, openApiMetadata: Doc("Create a credential (secret returned once)", "Credentials").WithRequestBody(Body<CredentialCreateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/credentials/{credentialId}", ReadAsync, null, openApiMetadata: Doc("Read a credential", "Credentials"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/credentials/{credentialId}", UpdateAsync, null, openApiMetadata: Doc("Update a credential", "Credentials").WithRequestBody(Body<CredentialUpdateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/credentials/{credentialId}", DeleteAsync, null, openApiMetadata: Doc("Delete a credential", "Credentials"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.CredentialRead).ConfigureAwait(false)) return;
            CredentialFilter filter = new CredentialFilter { TenantId = tenantId, UserId = Query(ctx, "userId"), Active = QueryBool(ctx, "active") };
            ApplyPaging(ctx, filter);
            EnumerationResult<Credential> creds = await Server.Database.Credentials.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, creds.Map(CredentialResponse.FromModel)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.CredentialWrite).ConfigureAwait(false)) return;
            CredentialCreateRequest? req = await ReadBodyAsync<CredentialCreateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            RequestContext rc = Context(ctx);
            string? userId = req.UserId ?? rc.UserId;
            if (String.IsNullOrWhiteSpace(req.Name) || String.IsNullOrEmpty(userId))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Name and an owning user are required.").ConfigureAwait(false);
                return;
            }

            User? owner = await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            if (owner == null)
            {
                await SendNotFoundAsync(ctx, "Owning user").ConfigureAwait(false);
                return;
            }

            string secret = SecretGenerator.SecretKey();
            Credential credential = new Credential
            {
                TenantId = tenantId,
                UserId = owner.Id,
                Name = req.Name.Trim(),
                AccessKey = SecretGenerator.AccessKey(),
                SecretKeyEncrypted = Server.Authentication.ProtectSecret(secret),
                SecretKeyLast4 = secret.Substring(secret.Length - 4),
                AuthMode = req.AuthMode,
                Scopes = (req.Scopes ?? new List<string>()).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Distinct().ToList(),
                ResourceIds = req.ResourceIds ?? new List<string>(),
                ExpiresUtc = req.ExpiresUtc
            };
            credential = await Server.Database.Credentials.CreateAsync(credential, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.CredentialCreated, "Credential " + credential.Id + " created.", 201, "credential_created").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, new CredentialCreatedResponse { Credential = CredentialResponse.FromModel(credential), SecretKey = secret }).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            string id = Param(ctx, "credentialId") ?? String.Empty;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.CredentialRead, id).ConfigureAwait(false)) return;
            Credential? credential = await Server.Database.Credentials.ReadAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
            if (credential == null)
            {
                await SendNotFoundAsync(ctx, "Credential").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, CredentialResponse.FromModel(credential)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            string id = Param(ctx, "credentialId") ?? String.Empty;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.CredentialWrite, id).ConfigureAwait(false)) return;
            CredentialUpdateRequest? req = await ReadBodyAsync<CredentialUpdateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Credential? credential = await Server.Database.Credentials.ReadAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
            if (credential == null)
            {
                await SendNotFoundAsync(ctx, "Credential").ConfigureAwait(false);
                return;
            }

            if (!String.IsNullOrWhiteSpace(req.Name)) credential.Name = req.Name.Trim();
            if (req.Scopes != null) credential.Scopes = req.Scopes.Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Distinct().ToList();
            if (req.ResourceIds != null) credential.ResourceIds = req.ResourceIds;
            if (req.ExpiresUtc.HasValue) credential.ExpiresUtc = req.ExpiresUtc;
            if (req.Active.HasValue) credential.Active = req.Active.Value;
            credential = await Server.Database.Credentials.UpdateAsync(credential, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.CredentialCreated, "Credential " + credential.Id + " updated.", 200, "credential_updated").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, CredentialResponse.FromModel(credential)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            string id = Param(ctx, "credentialId") ?? String.Empty;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.CredentialDelete, id).ConfigureAwait(false)) return;
            Credential? credential = await Server.Database.Credentials.ReadAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
            if (credential == null)
            {
                await SendNotFoundAsync(ctx, "Credential").ConfigureAwait(false);
                return;
            }

            if (credential.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "A protected credential cannot be deleted.").ConfigureAwait(false);
                return;
            }

            await Server.Database.Credentials.DeleteAsync(tenantId, id, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, AuditEventTypeEnum.CredentialRevoked, "Credential " + id + " deleted.", 204, "credential_rotated").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ChangedAsync(HttpContextBase ctx, AuditEventTypeEnum type, string description, int status, string mutation)
        {
            Server.Authorization.Invalidate();
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RbacMutations, "mutation", mutation);
            await Server.Audit.RecordAsync(type, Context(ctx), status, description, ctx.Token).ConfigureAwait(false);
        }
    }
}
