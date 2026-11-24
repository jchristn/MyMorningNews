namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Audio;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Audio and podcasts (plan section 22): episodes and chapters, generating audio for a briefing, voice profiles with a
    /// test sample, private feed tokens, the public podcast RSS feed with ranged episode downloads, and public artwork.
    /// </summary>
    public class MediaRoutes : RouteBase
    {
        /// <summary>
        /// Voice previews one user may request per minute. Default 20.
        /// </summary>
        public int PreviewsPerMinute { get; set; } = 20;

        private static readonly string[] _PublicAssets = { "glyph.png", "logo.png" };
        private readonly MyMorningNews.Core.Services.LoginRateLimiter _PreviewLimiter;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public MediaRoutes(ServerContext server) : base(server)
        {
            _PreviewLimiter = new MyMorningNews.Core.Services.LoginRateLimiter(PreviewsPerMinute);
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/media", ListAsync, null, openApiMetadata: Paged(Doc("List audio episodes", "Media").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("briefingId", typeof(string))).WithParameter(QueryParam("editionId", typeof(string))).WithParameter(QueryParam("status", typeof(MediaStatusEnum)))
                .WithParameter(QueryParam("kind", typeof(MediaKindEnum)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/media/{id}", ReadAsync, null, openApiMetadata: Doc("Read an episode with its chapters", "Media"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/media/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an episode and its files", "Media"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/briefings/{id}/audio", GenerateAsync, null, openApiMetadata: Doc("Make (or remake) audio for a briefing", "Media",
                "Returns 202 with the queued episode. 409 when audio for the briefing is already being made.").WithRequestBody(Body<AudioGenerateRequest>()));

            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/voices", ListVoicesAsync, null, openApiMetadata: Paged(Doc("List voice profiles", "Voices")));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/voices", CreateVoiceAsync, null, openApiMetadata: Doc("Create a voice profile", "Voices").WithRequestBody(Body<VoiceProfileUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/voices/{id}", ReadVoiceAsync, null, openApiMetadata: Doc("Read a voice profile", "Voices"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/voices/{id}", UpdateVoiceAsync, null, openApiMetadata: Doc("Update a voice profile", "Voices").WithRequestBody(Body<VoiceProfileUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/voices/{id}", DeleteVoiceAsync, null, openApiMetadata: Doc("Delete a voice profile", "Voices"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/voices/{id}/test", TestVoiceAsync, null, openApiMetadata: Doc("Speak a sample with a voice", "Voices",
                "Returns the audio (audio/wav). 502 TtsError when the speech endpoint fails.").WithRequestBody(Body<VoiceTestRequest>()));

            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/voices/available", AvailableVoicesAsync, null, openApiMetadata: Doc("List the voices a topic can use", "Voices",
                "The workspace's voice profiles, then the voices the speech server offers that have no profile yet (read from the speech endpoint's /audio/voices; the configured default voices when it has no listing). Each has a readable label. Anyone who can read topics may call it."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/voices/preview", PreviewVoiceAsync, null, openApiMetadata: Doc("Hear a short sample of a voice", "Voices",
                "Speaks up to 300 characters with a speech server voice (voice) or a voice profile (voiceProfileId) and returns the audio (audio/wav). Anyone who can edit topics may call it, up to 20 times a minute. 409 when speech is turned off, 429 TooManyRequests over the limit, 502 TtsError when the speech endpoint fails.").WithRequestBody(Body<VoicePreviewRequest>()));

            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/feed-tokens", ListTokensAsync, null, openApiMetadata: Doc("List my podcast feed tokens", "Podcast feeds"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/feed-tokens", CreateTokenAsync, null, openApiMetadata: Doc("Create a private podcast feed", "Podcast feeds",
                "Returns the secret and feed URL once; only a hash is stored.").WithRequestBody(Body<FeedTokenCreateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/feed-tokens/{id}", RevokeTokenAsync, null, openApiMetadata: Doc("Revoke a podcast feed token", "Podcast feeds", "Revokes an active token; with purge=true, deletes an already revoked one.").WithParameter(QueryParam("purge", typeof(bool))));

            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/feeds/{token}/podcast.xml", FeedAsync, null, openApiMetadata: Doc("Private podcast feed (RSS 2.0 with iTunes tags)", "Public"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/feeds/{token}/episodes/{file}", EpisodeAsync, null, openApiMetadata: Doc("Podcast episode audio (Range supported)", "Public"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/feeds/{token}/chapters/{file}", ChaptersAsync, null, openApiMetadata: Doc("Podcast episode chapters (Podcasting 2.0 JSON)", "Public"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/assets/{name}", AssetAsync, null, openApiMetadata: Doc("Public artwork (glyph.png, logo.png)", "Public"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.MediaRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            MediaAssetFilter filter = new MediaAssetFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                BriefingId = Query(ctx, "briefingId"),
                EditionId = Query(ctx, "editionId"),
                Status = QueryEnum<MediaStatusEnum>(ctx, "status"),
                Kind = QueryEnum<MediaKindEnum>(ctx, "kind")
            };
            ApplyPaging(ctx, filter);
            EnumerationResult<MediaAsset> page = await Server.Database.MediaAssets.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            foreach (MediaAsset m in page.Items) m.ScriptMarkdown = null;
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            MediaAsset? m = await OwnedMediaAsync(ctx, RequestTypeEnum.MediaRead).ConfigureAwait(false);
            if (m == null) return;
            await SendJsonAsync(ctx, 200, new MediaDetailResponse
            {
                Media = m,
                Chapters = await Server.Audio.ChaptersAsync(m, ctx.Token).ConfigureAwait(false),
                AudioPath = m.ArtifactId == null ? null : "/v1.0/api/artifacts/" + m.ArtifactId + "/content",
                ChaptersPath = m.ChaptersArtifactId == null ? null : "/v1.0/api/artifacts/" + m.ChaptersArtifactId + "/content"
            }).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            MediaAsset? m = await OwnedMediaAsync(ctx, RequestTypeEnum.MediaWrite).ConfigureAwait(false);
            if (m == null) return;
            await Server.Audio.DeleteAsync(m, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task GenerateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.MediaWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Briefing? b = await Server.Database.Briefings.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (b == null || !IsOwnerOrAdmin(ctx, b.UserId)) { await SendNotFoundAsync(ctx, "Briefing").ConfigureAwait(false); return; }
            AudioGenerateRequest req = (await ReadOptionalBodyAsync<AudioGenerateRequest>(ctx).ConfigureAwait(false)) ?? new AudioGenerateRequest();
            if (!Server.Settings.Tts.Enabled) { await SendErrorAsync(ctx, 409, "AudioDisabled", "Audio is turned off on this server (tts.enabled).").ConfigureAwait(false); return; }
            if (b.Status != BriefingStatusEnum.Ready) { await SendErrorAsync(ctx, 409, "BriefingNotReady", "Audio is made from a ready briefing.").ConfigureAwait(false); return; }
            await Server.Entitlements.CheckAudioAsync(b.TenantId, b.UserId, ctx.Token).ConfigureAwait(false);
            MediaAsset? busy = (await Server.Database.MediaAssets.EnumerateAsync(new MediaAssetFilter { TenantId = b.TenantId, BriefingId = b.Id, PageSize = 20 }, ctx.Token).ConfigureAwait(false)).Items
                .FirstOrDefault(x => x.Status == MediaStatusEnum.Queued || x.Status == MediaStatusEnum.Scripting || x.Status == MediaStatusEnum.Synthesizing);
            if (busy != null)
            {
                await SendErrorAsync(ctx, 409, "AudioInProgress", "Audio for this briefing is already being made.", new Dictionary<string, string> { { "mediaId", busy.Id }, { "status", busy.Status.ToString() } }).ConfigureAwait(false);
                return;
            }

            MediaAsset? m = await Server.Audio.QueueForBriefingAsync(b, b.Kind == BriefingKindEnum.OneTime ? MediaKindEnum.PodcastEpisode : MediaKindEnum.BriefingAudio, req.VoiceProfileId, true, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 202, m).ConfigureAwait(false);
        }

        private async Task ListVoicesAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VoiceRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            VoiceProfileFilter filter = new VoiceProfileFilter { TenantId = tenantId };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.VoiceProfiles.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadVoiceAsync(HttpContextBase ctx)
        {
            VoiceProfile? v = await VoiceAsync(ctx, RequestTypeEnum.VoiceRead).ConfigureAwait(false);
            if (v == null) return;
            await SendJsonAsync(ctx, 200, v).ConfigureAwait(false);
        }

        private async Task CreateVoiceAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.VoiceWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            VoiceProfileUpsertRequest? req = await ReadBodyAsync<VoiceProfileUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name) || String.IsNullOrWhiteSpace(req.Voice))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "name and voice are required.").ConfigureAwait(false);
                return;
            }

            VoiceProfile v = new VoiceProfile { TenantId = tenantId };
            if (!await ApplyVoiceAsync(ctx, v, req).ConfigureAwait(false)) return;
            v = await Server.Database.VoiceProfiles.CreateAsync(v, ctx.Token).ConfigureAwait(false);
            if (v.IsDefault) await ClearOtherDefaultsAsync(v, ctx).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, v).ConfigureAwait(false);
        }

        private async Task UpdateVoiceAsync(HttpContextBase ctx)
        {
            VoiceProfile? v = await VoiceAsync(ctx, RequestTypeEnum.VoiceWrite).ConfigureAwait(false);
            if (v == null) return;
            VoiceProfileUpsertRequest? req = await ReadBodyAsync<VoiceProfileUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (!await ApplyVoiceAsync(ctx, v, req).ConfigureAwait(false)) return;
            v = await Server.Database.VoiceProfiles.UpdateAsync(v, ctx.Token).ConfigureAwait(false);
            if (v.IsDefault) await ClearOtherDefaultsAsync(v, ctx).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, v).ConfigureAwait(false);
        }

        private async Task DeleteVoiceAsync(HttpContextBase ctx)
        {
            VoiceProfile? v = await VoiceAsync(ctx, RequestTypeEnum.VoiceWrite).ConfigureAwait(false);
            if (v == null) return;
            await Server.Database.VoiceProfiles.DeleteAsync(v.TenantId, v.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task TestVoiceAsync(HttpContextBase ctx)
        {
            VoiceProfile? v = await VoiceAsync(ctx, RequestTypeEnum.VoiceRead).ConfigureAwait(false);
            if (v == null) return;
            VoiceTestRequest req = (await ReadOptionalBodyAsync<VoiceTestRequest>(ctx).ConfigureAwait(false)) ?? new VoiceTestRequest();
            try
            {
                TtsResult r = await Server.Audio.SampleAsync(v, req.Text ?? String.Empty, ctx.Token, Context(ctx).UserId).ConfigureAwait(false);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = r.ContentType;
                ctx.Response.Headers.Add("Cache-Control", "no-store");
                await ctx.Response.Send(r.Audio, ctx.Token).ConfigureAwait(false);
            }
            catch (TtsException ex)
            {
                await SendErrorAsync(ctx, 502, "TtsError", ex.Message).ConfigureAwait(false);
            }
        }

        private async Task AvailableVoicesAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            await SendJsonAsync(ctx, 200, await Server.Audio.AvailableVoicesAsync(tenantId, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task PreviewVoiceAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            VoicePreviewRequest? req = await ReadBodyAsync<VoicePreviewRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            RequestContext rc = Context(ctx);
            _PreviewLimiter.LimitPerMinute = PreviewsPerMinute;
            if (!_PreviewLimiter.TryAcquire((rc.UserId ?? rc.Authentication.PrincipalId ?? "unknown") + "|voice-preview"))
            {
                await SendErrorAsync(ctx, 429, "TooManyRequests", "Too many voice previews. Try again in a minute.").ConfigureAwait(false);
                return;
            }

            try
            {
                TtsResult r = await Server.Audio.PreviewAsync(tenantId, req, ctx.Token, Context(ctx).UserId).ConfigureAwait(false);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = r.ContentType;
                ctx.Response.Headers.Add("Cache-Control", "no-store");
                await ctx.Response.Send(r.Audio, ctx.Token).ConfigureAwait(false);
            }
            catch (TtsException ex)
            {
                await SendErrorAsync(ctx, 502, "TtsError", ex.Message).ConfigureAwait(false);
            }
        }

        private async Task ListTokensAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedTokenRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            List<FeedToken> tokens = (await Server.Database.FeedTokens.EnumerateAsync(new FeedTokenFilter { TenantId = tenantId, UserId = Context(ctx).UserId, PageSize = 200 }, ctx.Token).ConfigureAwait(false)).Items
                .OrderByDescending(t => t.CreatedUtc).ToList();
            foreach (FeedToken t in tokens) t.TokenHash = "redacted";
            await SendJsonAsync(ctx, 200, tokens).ConfigureAwait(false);
        }

        private async Task CreateTokenAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedTokenWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            FeedTokenCreateRequest req = (await ReadOptionalBodyAsync<FeedTokenCreateRequest>(ctx).ConfigureAwait(false)) ?? new FeedTokenCreateRequest();
            RequestContext rc = Context(ctx);
            if (!String.IsNullOrEmpty(req.EditionId))
            {
                Edition? e = await Server.Database.Editions.ReadAsync(tenantId, req.EditionId, ctx.Token).ConfigureAwait(false);
                if (e == null || e.UserId != rc.UserId) { await SendErrorAsync(ctx, 400, "ValidationFailed", "The edition was not found among yours.").ConfigureAwait(false); return; }
            }

            FeedTokenCreated created = await Server.Audio.CreateFeedTokenAsync(tenantId, rc.UserId ?? String.Empty, String.IsNullOrEmpty(req.EditionId) ? null : req.EditionId, req.Name ?? String.Empty, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.FeedTokenCreated, rc, 201, "Feed token " + created.Token.Id + " (" + created.Token.TokenPrefix + ") created.", ctx.Token).ConfigureAwait(false);
            created.Token.TokenHash = "redacted";
            ctx.Response.Headers.Add("Cache-Control", "no-store");
            await SendJsonAsync(ctx, 201, created).ConfigureAwait(false);
        }

        private async Task RevokeTokenAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FeedTokenWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            FeedToken? t = await Server.Database.FeedTokens.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (t == null || !IsOwnerOrAdmin(ctx, t.UserId)) { await SendNotFoundAsync(ctx, "Feed token").ConfigureAwait(false); return; }
            if (t.RevokedUtc.HasValue && QueryBool(ctx, "purge") == true)
            {
                await Server.Database.FeedTokens.DeleteAsync(t.TenantId, t.Id, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
                return;
            }

            if (!t.RevokedUtc.HasValue)
            {
                t.RevokedUtc = DateTime.UtcNow;
                await Server.Database.FeedTokens.UpdateAsync(t, ctx.Token).ConfigureAwait(false);
                await Server.Audit.RecordAsync(AuditEventTypeEnum.FeedTokenRevoked, Context(ctx), 204, "Feed token " + t.Id + " (" + t.TokenPrefix + ") revoked.", ctx.Token).ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task FeedAsync(HttpContextBase ctx)
        {
            string secret = Param(ctx, "token") ?? String.Empty;
            FeedToken? ft = await Server.Audio.AuthenticateFeedAsync(secret, ctx.Token).ConfigureAwait(false);
            if (ft == null) { await SendNotFoundAsync(ctx, "Feed").ConfigureAwait(false); return; }
            string xml = await Server.Audio.BuildFeedAsync(ft, secret, ctx.Token).ConfigureAwait(false);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/rss+xml; charset=utf-8";
            ctx.Response.Headers.Add("Cache-Control", "private, max-age=300");
            ctx.Response.Headers.Add("X-Robots-Tag", "noindex, nofollow");
            await ctx.Response.Send(Encoding.UTF8.GetBytes(xml), ctx.Token).ConfigureAwait(false);
        }

        private async Task EpisodeAsync(HttpContextBase ctx)
        {
            MediaAsset? m = await FeedMediaAsync(ctx).ConfigureAwait(false);
            if (m == null || m.ArtifactId == null) { if (!ctx.Response.ResponseSent) await SendNotFoundAsync(ctx, "Episode").ConfigureAwait(false); return; }
            Artifact? a = await Server.Database.Artifacts.ReadAsync(m.TenantId, m.ArtifactId, ctx.Token).ConfigureAwait(false);
            if (a == null) { await SendNotFoundAsync(ctx, "Episode").ConfigureAwait(false); return; }
            ctx.Response.Headers.Add("X-Robots-Tag", "noindex, nofollow");
            await SendArtifactAsync(ctx, Server.Artifacts, a, false).ConfigureAwait(false);
        }

        private async Task ChaptersAsync(HttpContextBase ctx)
        {
            MediaAsset? m = await FeedMediaAsync(ctx).ConfigureAwait(false);
            if (m == null || m.ChaptersArtifactId == null) { if (!ctx.Response.ResponseSent) await SendNotFoundAsync(ctx, "Chapters").ConfigureAwait(false); return; }
            Artifact? a = await Server.Database.Artifacts.ReadAsync(m.TenantId, m.ChaptersArtifactId, ctx.Token).ConfigureAwait(false);
            if (a == null) { await SendNotFoundAsync(ctx, "Chapters").ConfigureAwait(false); return; }
            await SendArtifactAsync(ctx, Server.Artifacts, a, false).ConfigureAwait(false);
        }

        private async Task AssetAsync(HttpContextBase ctx)
        {
            string name = (Param(ctx, "name") ?? String.Empty).Trim().ToLowerInvariant();
            string path = Path.Combine(AppContext.BaseDirectory, "assets", name);
            if (!_PublicAssets.Contains(name) || !File.Exists(path)) { await SendNotFoundAsync(ctx, "Asset").ConfigureAwait(false); return; }
            byte[] data = await File.ReadAllBytesAsync(path, ctx.Token).ConfigureAwait(false);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "image/png";
            ctx.Response.Headers.Add("Cache-Control", "public, max-age=86400");
            await ctx.Response.Send(data, ctx.Token).ConfigureAwait(false);
        }

        private async Task<MediaAsset?> FeedMediaAsync(HttpContextBase ctx)
        {
            FeedToken? ft = await Server.Audio.AuthenticateFeedAsync(Param(ctx, "token"), ctx.Token).ConfigureAwait(false);
            if (ft == null) return null;
            string file = Param(ctx, "file") ?? String.Empty;
            int dot = file.IndexOf('.');
            string id = dot < 0 ? file : file.Substring(0, dot);
            return await Server.Audio.ReadFeedMediaAsync(ft, id, ctx.Token).ConfigureAwait(false);
        }

        private async Task<MediaAsset?> OwnedMediaAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            MediaAsset? m = await Server.Database.MediaAssets.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (m == null || !IsOwnerOrAdmin(ctx, m.UserId)) { await SendNotFoundAsync(ctx, "Episode").ConfigureAwait(false); return null; }
            return m;
        }

        private async Task<VoiceProfile?> VoiceAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            VoiceProfile? v = await Server.Database.VoiceProfiles.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (v == null) { await SendNotFoundAsync(ctx, "Voice").ConfigureAwait(false); return null; }
            return v;
        }

        private async Task<bool> ApplyVoiceAsync(HttpContextBase ctx, VoiceProfile v, VoiceProfileUpsertRequest req)
        {
            if (req.Name != null) v.Name = req.Name.Trim();
            if (req.Provider.HasValue) v.Provider = req.Provider.Value;
            if (req.Endpoint != null)
            {
                string ep = req.Endpoint.Trim();
                if (ep.Length > 0 && !(Uri.TryCreate(ep, UriKind.Absolute, out Uri? u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)))
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", "endpoint must be an http or https URL.").ConfigureAwait(false);
                    return false;
                }

                v.Endpoint = ep.Length == 0 ? null : ep.TrimEnd('/');
            }

            if (req.Model != null && req.Model.Trim().Length > 0) v.Model = req.Model.Trim();
            if (req.Voice != null && req.Voice.Trim().Length > 0) v.Voice = req.Voice.Trim();
            if (req.SecondaryVoice != null) v.SecondaryVoice = req.SecondaryVoice.Trim().Length == 0 ? null : req.SecondaryVoice.Trim();
            if (req.Speed.HasValue) v.Speed = req.Speed.Value;
            if (req.CredentialId != null)
            {
                if (req.CredentialId.Length == 0) v.CredentialId = null;
                else if (await Server.Database.VaultCredentials.ReadAsync(v.TenantId, req.CredentialId, ctx.Token).ConfigureAwait(false) == null)
                {
                    await SendErrorAsync(ctx, 400, "ValidationFailed", "The vault credential was not found.").ConfigureAwait(false);
                    return false;
                }
                else v.CredentialId = req.CredentialId;
            }

            if (req.IsDefault.HasValue) v.IsDefault = req.IsDefault.Value;
            return true;
        }

        private async Task ClearOtherDefaultsAsync(VoiceProfile v, HttpContextBase ctx)
        {
            foreach (VoiceProfile other in (await Server.Database.VoiceProfiles.EnumerateAsync(new VoiceProfileFilter { TenantId = v.TenantId, IsDefault = true, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Items.Where(o => o.Id != v.Id))
            {
                other.IsDefault = false;
                await Server.Database.VoiceProfiles.UpdateAsync(other, ctx.Token).ConfigureAwait(false);
            }
        }
    }
}
