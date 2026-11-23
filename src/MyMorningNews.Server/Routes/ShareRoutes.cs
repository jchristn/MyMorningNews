namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Public sharing of briefings (plan section 36). Signed-in users create, list, and revoke share links for a
    /// briefing they can read. Each share yields a reading page and a listening page that anyone with the link can
    /// open with no sign-in, plus the audio stream behind the listening page. Every link that does not work (unknown,
    /// revoked, expired, deleted, or sharing turned off) gets the same branded 404 page.
    /// </summary>
    public class ShareRoutes : RouteBase
    {
        private readonly LoginRateLimiter _Limiter;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public ShareRoutes(ServerContext server) : base(server)
        {
            _Limiter = new LoginRateLimiter(server.Settings.Sharing.PublicRequestsPerMinute);
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/briefings/{id}/shares";
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List the public share links of a briefing", "Sharing",
                "Newest first. Each share has its expiry, revocation time, view count, and the time it was last opened. The link secret is never returned here; it is shown once when the share is created.")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b, CreateAsync, null, openApiMetadata: Doc("Share a briefing through public links", "Sharing",
                "Creates one share and returns its link once (one page to listen and to read); only a hash of the secret is stored. Anyone with a link can open it with no sign-in until it expires or is revoked. " +
                "expiresInDays is 1 to 14 (the server maximum, sharing.maxExpiryDays); omitted, it is 7 (sharing.defaultExpiryDays). A value outside the range is refused with 400 ValidationFailed. " +
                "409 SharingDisabled when sharing is turned off on the server, 409 BriefingNotReady when the briefing is not ready.").WithRequestBody(Body<BriefingShareCreateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{shareId}", RevokeAsync, null, openApiMetadata: Doc("Revoke a public share link", "Sharing",
                "The link stops working at once. With purge=true, deletes a share that is already revoked or expired.").WithParameter(QueryParam("purge", typeof(bool))));

            const string p = "/v1.0/public/shares/{token}";
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, p, ReadPageAsync, null, openApiMetadata: Doc("Shared briefing page", "Public",
                "Server-rendered HTML: one page to listen and to read, with an audio player and chapter links when the briefing has finished audio, then the briefing text. No authentication. t starts playback at that many seconds. 404 with the same page for every link that does not work.")
                .WithParameter(QueryParam("t", typeof(int))));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, p + "/listen", ListenPageAsync, null, openApiMetadata: Doc("Shared briefing page (earlier listening address)", "Public",
                "Kept so links made when listening had its own page keep working. Renders the same single page as GET /v1.0/public/shares/{token}. No authentication. t starts playback at that many seconds.")
                .WithParameter(QueryParam("t", typeof(int))));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, p + "/audio/{file}", AudioAsync, null, openApiMetadata: Doc("Shared briefing: audio (Range supported)", "Public",
                "The audio behind the listening page. No authentication. Supports a single Range header (206 Partial Content)."));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.ShareRead).ConfigureAwait(false);
            if (br == null) return;
            BriefingShareFilter filter = new BriefingShareFilter { TenantId = br.TenantId, BriefingId = br.Id };
            ApplyPaging(ctx, filter);
            EnumerationResult<BriefingShare> page = await Server.Database.BriefingShares.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            foreach (BriefingShare s in page.Items) s.TokenHash = "redacted";
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.ShareWrite).ConfigureAwait(false);
            if (br == null) return;
            BriefingShareCreateRequest req = (await ReadOptionalBodyAsync<BriefingShareCreateRequest>(ctx).ConfigureAwait(false)) ?? new BriefingShareCreateRequest();
            if (!Server.Shares.Enabled) { await SendErrorAsync(ctx, 409, "SharingDisabled", "Public sharing is turned off on this server (sharing.enabled).").ConfigureAwait(false); return; }
            if (br.Status != BriefingStatusEnum.Ready) { await SendErrorAsync(ctx, 409, "BriefingNotReady", "Only a ready briefing can be shared.").ConfigureAwait(false); return; }
            int min = Server.Shares.MinExpiryDays;
            int max = Server.Shares.MaxExpiryDays;
            if (req.ExpiresInDays.HasValue && (req.ExpiresInDays.Value < min || req.ExpiresInDays.Value > max))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "expiresInDays must be between " + min + " and " + max + ".", new Dictionary<string, string>
                {
                    { "expiresInDays", "Must be between " + min + " and " + max + "." },
                    { "minimum", min.ToString(CultureInfo.InvariantCulture) },
                    { "maximum", max.ToString(CultureInfo.InvariantCulture) }
                }).ConfigureAwait(false);
                return;
            }

            RequestContext rc = Context(ctx);
            string userId = String.IsNullOrEmpty(rc.UserId) ? br.UserId : rc.UserId;
            BriefingShareCreated created = await Server.Shares.CreateAsync(br, userId, req.ExpiresInDays, req.ShowOwnerName == true, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.ShareCreated, rc, 201, "Share " + created.Share.Id + " (" + created.Share.TokenPrefix + ") created for briefing " + br.Id + ", expires "
                + created.Share.ExpiresUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) + ".", ctx.Token).ConfigureAwait(false);
            created.Share.TokenHash = "redacted";
            ctx.Response.Headers.Add("Cache-Control", "no-store");
            await SendJsonAsync(ctx, 201, created).ConfigureAwait(false);
        }

        private async Task RevokeAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.ShareWrite).ConfigureAwait(false);
            if (br == null) return;
            BriefingShare? s = await Server.Database.BriefingShares.ReadAsync(br.TenantId, Param(ctx, "shareId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (s == null || !String.Equals(s.BriefingId, br.Id, StringComparison.Ordinal)) { await SendNotFoundAsync(ctx, "Share").ConfigureAwait(false); return; }
            bool dead = s.RevokedUtc.HasValue || s.ExpiresUtc <= DateTime.UtcNow;
            if (dead && QueryBool(ctx, "purge") == true)
            {
                await Server.Database.BriefingShares.DeleteAsync(s.TenantId, s.Id, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
                return;
            }

            if (!s.RevokedUtc.HasValue)
            {
                s.RevokedUtc = DateTime.UtcNow;
                await Server.Database.BriefingShares.UpdateAsync(s, ctx.Token).ConfigureAwait(false);
                await Server.Audit.RecordAsync(AuditEventTypeEnum.ShareRevoked, Context(ctx), 204, "Share " + s.Id + " (" + s.TokenPrefix + ") for briefing " + br.Id + " revoked.", ctx.Token).ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ReadPageAsync(HttpContextBase ctx)
        {
            if (!await AllowAsync(ctx).ConfigureAwait(false)) return;
            string secret = Param(ctx, "token") ?? String.Empty;
            SharedBriefing? shared = await Server.Shares.ResolveAsync(secret, ctx.Token).ConfigureAwait(false);
            if (shared == null) { await SendHtmlAsync(ctx, 404, SharePages.NotFound(Links(null, null))).ConfigureAwait(false); return; }
            int? start = QueryInt(ctx, "t");
            // A chapter link reloads the page at another time; that is the same visit, not a new opening.
            if (!start.HasValue) await Server.Shares.RecordViewAsync(shared.Share, ctx.Token).ConfigureAwait(false);
            await SendHtmlAsync(ctx, 200, SharePages.Read(shared, Links(secret, shared), start)).ConfigureAwait(false);
        }

        private async Task ListenPageAsync(HttpContextBase ctx)
        {
            if (!await AllowAsync(ctx).ConfigureAwait(false)) return;
            string secret = Param(ctx, "token") ?? String.Empty;
            SharedBriefing? shared = await Server.Shares.ResolveAsync(secret, ctx.Token).ConfigureAwait(false);
            if (shared == null) { await SendHtmlAsync(ctx, 404, SharePages.NotFound(Links(null, null))).ConfigureAwait(false); return; }
            int? start = QueryInt(ctx, "t");
            // A chapter link reloads the page at another time; that is the same visit, not a new opening.
            if (!start.HasValue) await Server.Shares.RecordViewAsync(shared.Share, ctx.Token).ConfigureAwait(false);
            await SendHtmlAsync(ctx, 200, SharePages.Listen(shared, Links(secret, shared), start)).ConfigureAwait(false);
        }

        private async Task AudioAsync(HttpContextBase ctx)
        {
            SharedBriefing? shared = await Server.Shares.ResolveAsync(Param(ctx, "token"), ctx.Token).ConfigureAwait(false);
            if (shared == null || shared.AudioFile == null) { await SendHtmlAsync(ctx, 404, SharePages.NotFound(Links(null, null))).ConfigureAwait(false); return; }
            ctx.Response.Headers.Add("X-Robots-Tag", "noindex, nofollow");
            ctx.Response.Headers.Add("Referrer-Policy", "no-referrer");
            await SendArtifactAsync(ctx, Server.Artifacts, shared.AudioFile, false).ConfigureAwait(false);
        }

        private SharePageLinks Links(string? secret, SharedBriefing? shared)
        {
            string baseUrl = Server.Settings.Rest.PublicBaseUrl;
            string root = Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? u) ? u.AbsolutePath.TrimEnd('/') : String.Empty;
            bool signup = Server.Settings.Hosting.PublicSignupEnabled;
            SharePageLinks links = new SharePageLinks
            {
                LogoPath = root + "/v1.0/public/assets/logo.png",
                LogoUrl = baseUrl + "/v1.0/public/assets/logo.png",
                SignupOpen = signup,
                SignupUrl = signup ? Server.Settings.Rest.AppBaseUrl + "/signup" : Constants.MarketingSiteUrl,
                SiteUrl = Constants.MarketingSiteUrl
            };
            if (secret != null)
            {
                links.ReadPath = root + BriefingShareService.PagePath(secret);
                links.ListenPath = links.ReadPath + "/listen";
                if (shared != null && shared.AudioFile != null) links.AudioPath = links.ReadPath + "/audio/" + BriefingShareService.AudioFileName(shared.AudioFile);
            }

            return links;
        }

        private async Task<bool> AllowAsync(HttpContextBase ctx)
        {
            string ip = Context(ctx).SourceIp ?? ctx.Request.Source?.IpAddress ?? "unknown";
            _Limiter.LimitPerMinute = Server.Settings.Sharing.PublicRequestsPerMinute;
            if (_Limiter.TryAcquire("share:" + ip)) return true;
            ctx.Response.Headers.Add("Retry-After", "60");
            await SendHtmlAsync(ctx, 429, SharePages.TooMany(Links(null, null))).ConfigureAwait(false);
            return false;
        }

        private async Task<Briefing?> OwnedAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Briefing? br = await Server.Database.Briefings.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (br == null || !IsOwnerOrAdmin(ctx, br.UserId))
            {
                await SendNotFoundAsync(ctx, "Briefing").ConfigureAwait(false);
                return null;
            }

            return br;
        }

        private static async Task SendHtmlAsync(HttpContextBase ctx, int status, string html)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.Add("Cache-Control", "no-store");
            ctx.Response.Headers.Add("X-Robots-Tag", "noindex, nofollow");
            ctx.Response.Headers.Add("Referrer-Policy", "no-referrer");
            ctx.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            ctx.Response.Headers.Add("Content-Security-Policy", SharePages.ContentSecurityPolicy);
            await ctx.Response.Send(Encoding.UTF8.GetBytes(html), ctx.Token).ConfigureAwait(false);
        }
    }
}
