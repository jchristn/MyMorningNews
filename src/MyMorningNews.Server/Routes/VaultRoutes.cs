namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Credential vault routes. Reads never return secrets; the reveal action is audited, rate limited, and never cached.
    /// </summary>
    public class VaultRoutes : RouteBase
    {
        /// <summary>
        /// Reveals allowed per user per minute. Default 10.
        /// </summary>
        public int RevealsPerMinute { get; set; } = 10;

        private readonly LoginRateLimiter _RevealLimiter;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public VaultRoutes(ServerContext server) : base(server)
        {
            _RevealLimiter = new LoginRateLimiter(RevealsPerMinute);
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            string b = "/v1.0/api/vault/credentials";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List vault credentials (no secrets)", "Vault").WithParameter(QueryParam("active", typeof(bool))).WithParameter(QueryParam("kind", typeof(VaultCredentialKindEnum))).WithParameter(QueryParam("provider", typeof(VaultProviderEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b, CreateAsync, null, openApiMetadata: Doc("Store a credential", "Vault").WithRequestBody(Body<VaultCredentialUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a credential (no secret)", "Vault"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update or rotate a credential", "Vault").WithRequestBody(Body<VaultCredentialUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a credential", "Vault"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/reveal", RevealAsync, null, openApiMetadata: Doc("Reveal a stored secret (audited)", "Vault"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/usage", UsageAsync, null, openApiMetadata: Doc("Records that use a credential", "Vault"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            VaultCredentialFilter filter = new VaultCredentialFilter { TenantId = tenantId, Kind = QueryEnum<VaultCredentialKindEnum>(ctx, "kind"), Provider = QueryEnum<VaultProviderEnum>(ctx, "provider"), Active = QueryBool(ctx, "active") };
            ApplyPaging(ctx, filter);
            EnumerationResult<VaultCredential> page = await Server.Database.VaultCredentials.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, page.Map(VaultCredentialResponse.FromModel)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            VaultCredentialUpsertRequest? req = await ReadBodyAsync<VaultCredentialUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (String.IsNullOrWhiteSpace(req.Name)) errors["name"] = "Required.";
            if (String.IsNullOrEmpty(req.Secret)) errors["secret"] = "Required.";
            if (req.Kind == VaultCredentialKindEnum.CustomHeader && String.IsNullOrWhiteSpace(req.HeaderName)) errors["headerName"] = "Required for CustomHeader secrets.";
            if (req.Kind == VaultCredentialKindEnum.BasicAuth && String.IsNullOrWhiteSpace(req.Username)) errors["username"] = "Required for BasicAuth secrets.";
            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The credential is not valid.", errors).ConfigureAwait(false);
                return;
            }

            VaultCredential created = await Server.Vault.CreateAsync(tenantId, Context(ctx).UserId, req, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.VaultCredentialChanged, Context(ctx), 201, "Vault credential " + created.Id + " created.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, VaultCredentialResponse.FromModel(created)).ConfigureAwait(false);
        }

        private async Task<VaultCredential?> LoadAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            VaultCredential? v = await Server.Database.VaultCredentials.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (v == null) await SendNotFoundAsync(ctx, "Vault credential").ConfigureAwait(false);
            return v;
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultRead, Param(ctx, "id")).ConfigureAwait(false)) return;
            VaultCredential? v = await LoadAsync(ctx).ConfigureAwait(false);
            if (v == null) return;
            await SendJsonAsync(ctx, 200, VaultCredentialResponse.FromModel(v)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultWrite, Param(ctx, "id")).ConfigureAwait(false)) return;
            VaultCredentialUpsertRequest? req = await ReadBodyAsync<VaultCredentialUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            VaultCredential? v = await LoadAsync(ctx).ConfigureAwait(false);
            if (v == null) return;
            v = await Server.Vault.UpdateAsync(v, req, ctx.Token).ConfigureAwait(false);
            if (!String.IsNullOrEmpty(req.Secret))
            {
                EnumerationResult<ModelEndpoint> rotated = await Server.Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = v.TenantId, CredentialId = v.Id, PageSize = 1000 }, ctx.Token).ConfigureAwait(false);
                foreach (ModelEndpoint e in rotated.Items)
                {
                    e.LastValidationSucceeded = null;
                    await Server.Database.ModelEndpoints.UpdateAsync(e, ctx.Token).ConfigureAwait(false);
                }
            }

            await Server.Audit.RecordAsync(AuditEventTypeEnum.VaultCredentialChanged, Context(ctx), 200, "Vault credential " + v.Id + (String.IsNullOrEmpty(req.Secret) ? " updated." : " rotated."), ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, VaultCredentialResponse.FromModel(v)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultDelete, Param(ctx, "id")).ConfigureAwait(false)) return;
            VaultCredential? v = await LoadAsync(ctx).ConfigureAwait(false);
            if (v == null) return;
            EnumerationResult<ModelEndpoint> users = await Server.Database.ModelEndpoints.EnumerateAsync(new ModelEndpointFilter { TenantId = v.TenantId, CredentialId = v.Id, PageSize = 1000 }, ctx.Token).ConfigureAwait(false);
            foreach (ModelEndpoint e in users.Items)
            {
                e.CredentialId = null;
                e.LastValidationSucceeded = null;
                await Server.Database.ModelEndpoints.UpdateAsync(e, ctx.Token).ConfigureAwait(false);
            }

            await Server.Database.VaultCredentials.DeleteAsync(v.TenantId, v.Id, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.VaultCredentialChanged, Context(ctx), 204, "Vault credential " + v.Id + " deleted.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task RevealAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultReveal, Param(ctx, "id")).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            if (!_RevealLimiter.TryAcquire((rc.UserId ?? rc.Authentication.PrincipalId ?? "unknown") + "|reveal"))
            {
                await SendErrorAsync(ctx, 429, "TooManyRequests", "Too many reveals. Try again in a minute.").ConfigureAwait(false);
                return;
            }

            VaultCredential? v = await LoadAsync(ctx).ConfigureAwait(false);
            if (v == null) return;
            string secret;
            try
            {
                secret = await Server.Vault.RevealAsync(v, ctx.Token).ConfigureAwait(false);
            }
            catch (VaultSecretUnavailableException ex)
            {
                await SendErrorAsync(ctx, 409, "Conflict", ex.Message).ConfigureAwait(false);
                return;
            }

            await Server.Audit.RecordAsync(AuditEventTypeEnum.CredentialRevealed, rc, 200, "Vault credential " + v.Id + " (" + v.Name + ") revealed.", ctx.Token).ConfigureAwait(false);
            ctx.Response.Headers.Set("Cache-Control", "no-store");
            ctx.Response.Headers.Set("Pragma", "no-cache");
            await SendJsonAsync(ctx, 200, new VaultRevealResponse { Id = v.Id, Name = v.Name, Secret = secret, Username = v.Username, RevealedUtc = DateTime.UtcNow }).ConfigureAwait(false);
        }

        private async Task UsageAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VaultRead, Param(ctx, "id")).ConfigureAwait(false)) return;
            VaultCredential? v = await LoadAsync(ctx).ConfigureAwait(false);
            if (v == null) return;
            await SendJsonAsync(ctx, 200, await Server.Vault.WhereUsedAsync(v.TenantId, v.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
