namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using HttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Hosted accounts (plan section 33.1): public sign-up, email verification, password reset, and, for signed-in
    /// users, the usage meter, data export, and account deletion. Public routes answer the same way whether or not an
    /// account exists. Universal link files for the mobile app are served here too.
    /// </summary>
    public class AccountRoutes : RouteBase
    {
        private static readonly HttpClient _Turnstile = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public AccountRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/signup", SignupAsync, null, openApiMetadata: Doc("Sign up for the hosted service", "Account",
                "Creates a personal tenant and an unverified account, then emails a verification link. Always answers 202 with the same body, whether or not the email has an account. 404 when sign-up is closed.").WithRequestBody(Body<SignupRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/verify-email", VerifyAsync, null, openApiMetadata: Doc("Verify an email address from the emailed link", "Account").WithRequestBody(Body<AccountTokenRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/verify-email/resend", ResendAsync, null, openApiMetadata: Doc("Send a new verification link", "Account", "Always 202.").WithRequestBody(Body<AccountEmailRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/password/forgot", ForgotAsync, null, openApiMetadata: Doc("Email a password reset link", "Account", "Always 202.").WithRequestBody(Body<AccountEmailRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/public/password/reset", ResetAsync, null, openApiMetadata: Doc("Set a new password from a reset link", "Account",
                "Signs out every session and emails a confirmation.").WithRequestBody(Body<AccountTokenRequest>()));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/.well-known/apple-app-site-association", AppleAsync, null, openApiMetadata: Doc("iOS universal links", "Public", "404 until Hosting.AppleTeamId is set."));
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/.well-known/assetlinks.json", AndroidAsync, null, openApiMetadata: Doc("Android app links", "Public", "404 until Hosting.AndroidCertificateFingerprints is set."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/account/usage", UsageAsync, null, openApiMetadata: Doc("My usage against my plan's limits", "Account"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/account/export", ExportAsync, null, openApiMetadata: Doc("Download my data (ZIP of JSON and Markdown)", "Account"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/account/delete", DeleteAsync, null, openApiMetadata: Doc("Delete my account", "Account",
                "Signs out every session and schedules the purge after Hosting.DeletionGraceDays (default 7); signing in again before then cancels it. Returns 202 with purgeAfterUtc."));
        }

        private async Task SignupAsync(HttpContextBase ctx)
        {
            if (!Server.Settings.Hosting.PublicSignupEnabled) { await SendErrorAsync(ctx, 404, "SignupClosed", "Sign-up is not open on this server.").ConfigureAwait(false); return; }
            SignupRequest? req = await ReadBodyAsync<SignupRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string? ip = Context(ctx).SourceIp;
            if (!await TurnstileAsync(req.TurnstileToken, ip).ConfigureAwait(false))
            {
                await SendErrorAsync(ctx, 400, "VerificationFailed", "The human check failed. Try again.").ConfigureAwait(false);
                return;
            }

            await Server.Account.SignUpAsync(req, ip, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, new Dictionary<string, object?>
            {
                { "message", Server.Settings.Hosting.RequireEmailVerification ? "Check your email for a link to confirm your address." : "Your account is ready. Sign in." },
                { "verificationRequired", Server.Settings.Hosting.RequireEmailVerification }
            }).ConfigureAwait(false);
        }

        private async Task VerifyAsync(HttpContextBase ctx)
        {
            AccountTokenRequest? req = await ReadBodyAsync<AccountTokenRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            User user = await Server.Account.VerifyEmailAsync(req.Token, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new Dictionary<string, object?> { { "verified", true }, { "email", user.Email } }).ConfigureAwait(false);
        }

        private async Task ResendAsync(HttpContextBase ctx)
        {
            AccountEmailRequest? req = await ReadBodyAsync<AccountEmailRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await Server.Account.ResendVerificationAsync(req.Email, Context(ctx).SourceIp, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, new Dictionary<string, object?> { { "message", "If that address has an unconfirmed account, a new link is on its way." } }).ConfigureAwait(false);
        }

        private async Task ForgotAsync(HttpContextBase ctx)
        {
            AccountEmailRequest? req = await ReadBodyAsync<AccountEmailRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await Server.Account.ForgotPasswordAsync(req.Email, Context(ctx).SourceIp, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, new Dictionary<string, object?> { { "message", "If that address has an account, a reset link is on its way." } }).ConfigureAwait(false);
        }

        private async Task ResetAsync(HttpContextBase ctx)
        {
            AccountTokenRequest? req = await ReadBodyAsync<AccountTokenRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            User user = await Server.Account.ResetPasswordAsync(req.Token, req.Password, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new Dictionary<string, object?> { { "reset", true }, { "email", user.Email } }).ConfigureAwait(false);
        }

        private async Task UsageAsync(HttpContextBase ctx)
        {
            User? user = await SelfAsync(ctx, RequestTypeEnum.UserSelfRead).ConfigureAwait(false);
            if (user == null) return;
            await SendJsonAsync(ctx, 200, await Server.Entitlements.MeterAsync(user.TenantId, user.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ExportAsync(HttpContextBase ctx)
        {
            User? user = await SelfAsync(ctx, RequestTypeEnum.UserSelfRead).ConfigureAwait(false);
            if (user == null) return;
            byte[] zip = await new AccountExportService(Server.Database).ExportAsync(user, ctx.Token).ConfigureAwait(false);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/zip";
            ctx.Response.Headers.Set("Content-Disposition", "attachment; filename=\"mymorningnews-export-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".zip\"");
            ctx.Response.Headers.Set("Cache-Control", "no-store");
            await ctx.Response.Send(zip, ctx.Token).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            User? user = await SelfAsync(ctx, RequestTypeEnum.UserSelfWrite).ConfigureAwait(false);
            if (user == null) return;
            DateTime purge = await Server.Account.RequestDeletionAsync(user, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, new Dictionary<string, object?> { { "purgeAfterUtc", purge }, { "message", "Your account is scheduled for deletion. Sign in before then to keep it." } }).ConfigureAwait(false);
        }

        private async Task AppleAsync(HttpContextBase ctx)
        {
            string team = Server.Settings.Hosting.AppleTeamId?.Trim() ?? String.Empty;
            if (team.Length == 0) { await SendErrorAsync(ctx, 404, "NotConfigured", "Universal links are not configured.").ConfigureAwait(false); return; }
            string appId = team + "." + Server.Settings.Hosting.MobileAppId;
            string[] paths = new[] { "/briefings/*", "/topics/*", "/predictions/*", "/findings/*", "/redeem*" };
            object doc = new Dictionary<string, object>
            {
                { "applinks", new Dictionary<string, object> { { "details", new[] { new Dictionary<string, object> { { "appIDs", new[] { appId } }, { "components", paths.Select(p => new Dictionary<string, string> { { "/", p } }).ToArray() } } } } } },
                { "webcredentials", new Dictionary<string, object> { { "apps", new[] { appId } } } }
            };
            await SendJsonAsync(ctx, 200, doc).ConfigureAwait(false);
        }

        private async Task AndroidAsync(HttpContextBase ctx)
        {
            List<string> prints = Server.Settings.Hosting.AndroidCertificateFingerprints.Where(f => !String.IsNullOrWhiteSpace(f)).Select(f => f.Trim().ToUpperInvariant()).ToList();
            if (prints.Count == 0) { await SendErrorAsync(ctx, 404, "NotConfigured", "App links are not configured.").ConfigureAwait(false); return; }
            object doc = new[]
            {
                new Dictionary<string, object>
                {
                    { "relation", new[] { "delegate_permission/common.handle_all_urls" } },
                    { "target", new Dictionary<string, object> { { "namespace", "android_app" }, { "package_name", Server.Settings.Hosting.MobileAppId }, { "sha256_cert_fingerprints", prints } } }
                }
            };
            await SendJsonAsync(ctx, 200, doc).ConfigureAwait(false);
        }

        private async Task<User?> SelfAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            RequestContext rc = Context(ctx);
            if (rc.Authentication.User == null || rc.CredentialId != null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Account routes need a signed-in user, not an access key.").ConfigureAwait(false);
                return null;
            }

            return rc.Authentication.User;
        }

        private async Task<bool> TurnstileAsync(string? response, string? ip)
        {
            string secret = Server.Settings.Hosting.TurnstileSecretKey;
            if (String.IsNullOrWhiteSpace(secret)) return true;
            if (String.IsNullOrWhiteSpace(response)) return false;
            Dictionary<string, string> form = new Dictionary<string, string> { { "secret", secret }, { "response", response } };
            if (!String.IsNullOrEmpty(ip)) form["remoteip"] = ip;
            try
            {
                using (FormUrlEncodedContent content = new FormUrlEncodedContent(form))
                using (HttpResponseMessage r = await _Turnstile.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content).ConfigureAwait(false))
                using (JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync().ConfigureAwait(false)))
                {
                    return doc.RootElement.TryGetProperty("success", out JsonElement ok) && ok.ValueKind == JsonValueKind.True;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                Server.Logging.Warn("[AccountRoutes] Turnstile check failed: " + ex.Message);
                return false;
            }
        }
    }
}
