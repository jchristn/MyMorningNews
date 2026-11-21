namespace MyMorningNews.Server.Routes
{
    using System;
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
    /// Session token routes: issue (anonymous, login headers), validate, details, refresh, and revoke.
    /// </summary>
    public class TokenRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public TokenRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/token", CreateAsync, null,
                openApiMetadata: Doc("Issue a session token", "Token", "Send x-email and x-password (x-tenant-guid optional: without it, a single matching tenant signs in and several return 409 with the tenant list), or x-access-key, x-secret-key, and x-tenant-guid. Send x-client: mobile to also receive a rotating refresh token."));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/token/refresh-token", RefreshWithTokenAsync, null,
                openApiMetadata: Doc("Exchange a mobile refresh token", "Token", "Returns a new session token and a rotated refresh token. Reusing a rotated refresh token revokes the sign-in.").WithRequestBody(Body<RefreshTokenRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/token", ValidateAsync, null, openApiMetadata: Doc("Validate the current token", "Token"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/token/details", DetailsAsync, null, openApiMetadata: Doc("Token and principal details", "Token"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/token/refresh", RefreshAsync, null, openApiMetadata: Doc("Refresh the current token", "Token"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.DELETE, "/v1.0/token", RevokeAsync, null, openApiMetadata: Doc("Revoke the current token (logout)", "Token"));
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            rc.RequestType = RequestTypeEnum.TokenCreate;
            LoginResult result = await Server.Authentication.LoginAsync(new AuthenticationRequest
            {
                GetHeader = name => ctx.Request.RetrieveHeaderValue(name),
                SourceIp = rc.SourceIp,
                UserAgent = rc.UserAgent
            }, ctx.Token).ConfigureAwait(false);

            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthRequests, "scheme", "login", "result", result.Outcome.ToString());
            if (!result.Success && result.StatusCode == 409 && result.TenantChoices != null)
            {
                TenantSelectionResponse choose = new TenantSelectionResponse();
                foreach (Tenant t in result.TenantChoices) choose.Tenants.Add(new TenantChoice { Id = t.Id, Name = t.Name });
                await SendJsonAsync(ctx, 409, choose).ConfigureAwait(false);
                return;
            }

            if (!result.Success)
            {
                string error = result.StatusCode == 429 ? "TooManyRequests" : result.StatusCode == 400 ? "BadRequest" : result.StatusCode == 403 ? "EmailNotVerified" : "Unauthorized";
                await SendErrorAsync(ctx, result.StatusCode, error, result.Message).ConfigureAwait(false);
                return;
            }

            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SessionEvents, "event", "issued", "principal_type", result.Session!.PrincipalType.ToString());
            rc.TenantId = result.Tenant?.Id;
            rc.UserId = result.User?.Id;
            rc.DisplayName = result.Credential != null ? "credential:" + result.Credential.Name : result.User?.Email;
            await SendJsonAsync(ctx, 200, ToResponse(result)).ConfigureAwait(false);
        }

        private async Task RefreshWithTokenAsync(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            rc.RequestType = RequestTypeEnum.TokenRefresh;
            RefreshTokenRequest? req = await ReadBodyAsync<RefreshTokenRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            LoginResult result = await Server.Authentication.RefreshWithTokenAsync(req.RefreshToken, rc.SourceIp, rc.UserAgent, ctx.Token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.AuthRequests, "scheme", "refresh_token", "result", result.Outcome.ToString());
            if (!result.Success)
            {
                await SendErrorAsync(ctx, result.StatusCode, result.StatusCode == 400 ? "BadRequest" : "Unauthorized", result.Message).ConfigureAwait(false);
                return;
            }

            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SessionEvents, "event", "refreshed", "principal_type", "User");
            rc.TenantId = result.Tenant?.Id;
            rc.UserId = result.User?.Id;
            await SendJsonAsync(ctx, 200, ToResponse(result)).ConfigureAwait(false);
        }

        private async Task ValidateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TokenRead).ConfigureAwait(false)) return;
            await SendJsonAsync(ctx, 200, Details(Context(ctx))).ConfigureAwait(false);
        }

        private async Task DetailsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TokenRead).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            TokenDetailsResponse details = Details(rc);
            string? raw = ctx.Request.RetrieveHeaderValue("authorization");
            string? token = raw != null && raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? raw.Substring(7).Trim() : ctx.Request.RetrieveHeaderValue("x-token");
            SessionTokenPayload? payload = Server.Authentication.Decode(token);
            if (payload != null)
            {
                details.IssuedUtc = payload.IssuedUtc;
                details.ExpiresUtc = payload.ExpiresUtc;
            }

            await SendJsonAsync(ctx, 200, details).ConfigureAwait(false);
        }

        private async Task RefreshAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TokenRefresh).ConfigureAwait(false)) return;
            LoginResult result = await Server.Authentication.RefreshAsync(Context(ctx), ctx.Token).ConfigureAwait(false);
            if (!result.Success)
            {
                await SendErrorAsync(ctx, result.StatusCode, "BadRequest", result.Message).ConfigureAwait(false);
                return;
            }

            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SessionEvents, "event", "refreshed", "principal_type", result.Session!.PrincipalType.ToString());
            await SendJsonAsync(ctx, 200, ToResponse(result)).ConfigureAwait(false);
        }

        private async Task RevokeAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TokenDelete).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            if (rc.SessionId == null || rc.TenantId == null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Only session tokens can be revoked.").ConfigureAwait(false);
                return;
            }

            await Server.Authentication.RevokeAsync(rc.TenantId, rc.SessionId, "Logout.", ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.SessionRevoked, rc, 204, "Logout.", ctx.Token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.SessionEvents, "event", "revoked", "principal_type", rc.Authentication.PrincipalType?.ToString() ?? "unknown");
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private static TokenResponse ToResponse(LoginResult result)
        {
            return new TokenResponse
            {
                Token = result.Token!,
                ExpiresUtc = result.Session!.ExpiresUtc,
                TenantId = result.Tenant!.Id,
                SessionId = result.Session.Id,
                UserId = result.User?.Id,
                CredentialId = result.Credential?.Id,
                PrincipalType = result.Session.PrincipalType,
                User = result.User != null && result.Credential == null ? UserResponse.FromModel(result.User) : null,
                RefreshToken = result.RefreshToken,
                RefreshExpiresUtc = result.RefreshExpiresUtc,
                DeletionCancelled = result.DeletionCancelled
            };
        }

        private static TokenDetailsResponse Details(RequestContext rc)
        {
            return new TokenDetailsResponse
            {
                Valid = rc.IsAuthenticated,
                SessionId = rc.SessionId,
                PrincipalType = rc.Authentication.PrincipalType,
                Scheme = rc.Authentication.Scheme,
                TenantId = rc.TenantId,
                TenantName = rc.Authentication.Tenant?.Name,
                UserId = rc.UserId,
                CredentialId = rc.CredentialId,
                Email = rc.Email,
                DisplayName = rc.DisplayName,
                IsAdmin = rc.IsAdmin,
                IsTenantAdmin = rc.IsTenantAdmin
            };
        }
    }
}
