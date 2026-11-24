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
    /// Briefings and their stored files (plan sections 21 and 22.1): list, read with sections and items, download
    /// Markdown or HTML, pin, regenerate expired files, delete, and stream any artifact with byte ranges.
    /// Members see their own briefings; tenant administrators see all in the tenant.
    /// </summary>
    public class BriefingRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public BriefingRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/briefings";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List briefings", "Briefings", "Newest first. Dry runs and previews are excluded unless includeDryRun=true.").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("kind", typeof(BriefingKindEnum))).WithParameter(QueryParam("editionId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string)))
                .WithParameter(QueryParam("runId", typeof(string))).WithParameter(QueryParam("status", typeof(BriefingStatusEnum))).WithParameter(QueryParam("pinned", typeof(bool)))
                .WithParameter(QueryParam("includeDryRun", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List briefings", "Briefings", "Newest first. Dry runs and previews are excluded unless includeDryRun=true.").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("kind", typeof(BriefingKindEnum))).WithParameter(QueryParam("editionId", typeof(string))).WithParameter(QueryParam("topicId", typeof(string)))
                .WithParameter(QueryParam("runId", typeof(string))).WithParameter(QueryParam("status", typeof(BriefingStatusEnum))).WithParameter(QueryParam("pinned", typeof(bool)))
                .WithParameter(QueryParam("includeDryRun", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a briefing with its sections, items, and files", "Briefings"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/download", DownloadAsync, null, openApiMetadata: Doc("Download a briefing as Markdown or HTML", "Briefings",
                "format=md (default) or html. 410 ArtifactExpired when the file passed retention; regenerate to restore it.").WithParameter(QueryParam("format", typeof(string))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/progress", ProgressAsync, null, openApiMetadata: Doc("Where I stopped listening", "Briefings",
                "The caller's position in seconds and whether they finished. Position 0 and completed false when nothing is saved."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}/progress", SaveProgressAsync, null, openApiMetadata: Doc("Save where I stopped listening", "Briefings",
                "Used by the mobile player so listening resumes on any device.").WithRequestBody(Body<BriefingProgressRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}/pin", PinAsync, null, openApiMetadata: Doc("Pin or unpin a briefing past the retention window", "Briefings")
                .WithRequestBody(Body<BriefingPinRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/regenerate", RegenerateAsync, null, openApiMetadata: Doc("Store fresh Markdown and HTML files from the saved briefing", "Briefings"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a briefing and its files", "Briefings"));

            const string a = "/v1.0/api/artifacts";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, a, ListArtifactsAsync, null, openApiMetadata: Paged(Doc("List stored files", "Artifacts").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("briefingId", typeof(string))).WithParameter(QueryParam("kind", typeof(ArtifactKindEnum))).WithParameter(QueryParam("pinned", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, a + "/enumerate", ctx => EnumerateAsync(ctx, ListArtifactsAsync), null, openApiMetadata: Paged(Doc("List stored files", "Artifacts").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("briefingId", typeof(string))).WithParameter(QueryParam("kind", typeof(ArtifactKindEnum))).WithParameter(QueryParam("pinned", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, a + "/{id}", ReadArtifactAsync, null, openApiMetadata: Doc("Read stored file metadata", "Artifacts"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, a + "/{id}/content", ContentAsync, null, openApiMetadata: Doc("Stream a stored file", "Artifacts",
                "Supports a single Range header (206 Partial Content) for audio seeking. download=true sends it as an attachment.").WithParameter(QueryParam("download", typeof(bool))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, a + "/{id}/download-url", DownloadUrlAsync, null, openApiMetadata: Doc("A presigned download URL when the store offers one", "Artifacts"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            BriefingFilter filter = new BriefingFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                Kind = QueryEnum<BriefingKindEnum>(ctx, "kind"),
                EditionId = Query(ctx, "editionId"),
                TopicId = Query(ctx, "topicId"),
                RunId = Query(ctx, "runId"),
                Status = QueryEnum<BriefingStatusEnum>(ctx, "status"),
                Pinned = QueryBool(ctx, "pinned"),
                IsDryRun = QueryBool(ctx, "includeDryRun") == true ? null : false
            };
            ApplyPaging(ctx, filter);
            if (String.IsNullOrEmpty(filter.SortBy)) filter.SortBy = "createdUtc";

            EnumerationResult<Briefing> page = await Server.Database.Briefings.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            foreach (Briefing x in page.Objects) x.Markdown = null;
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false);
            if (br == null) return;
            BriefingDetailResponse detail = new BriefingDetailResponse { Briefing = br };
            detail.Sections = (await Server.Database.BriefingSections.EnumerateAsync(new BriefingSectionFilter { TenantId = br.TenantId, BriefingId = br.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Objects
                .OrderBy(s => s.SortOrder).ToList();
            detail.Items = (await Server.Database.BriefingItems.EnumerateAsync(new BriefingItemFilter { TenantId = br.TenantId, BriefingId = br.Id, PageSize = 1000 }, ctx.Token).ConfigureAwait(false)).Objects
                .OrderBy(i => detail.Sections.FindIndex(s => s.Id == i.SectionId)).ThenBy(i => i.SortOrder).ToList();
            detail.Files = (await Server.Database.Artifacts.EnumerateAsync(new ArtifactFilter { TenantId = br.TenantId, BriefingId = br.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Objects;
            detail.FilesExpired = br.Status == BriefingStatusEnum.Ready && !br.IsDryRun &&
                new[] { br.MarkdownArtifactId, br.HtmlArtifactId }.Any(id => id == null || detail.Files.FirstOrDefault(f => f.Id == id)?.DeletedUtc != null || detail.Files.All(f => f.Id != id));
            if (br.EditionId != null) detail.EditionName = (await Server.Database.Editions.ReadAsync(br.TenantId, br.EditionId, ctx.Token).ConfigureAwait(false))?.Name;
            if (br.TopicId != null) detail.TopicName = (await Server.Database.Topics.ReadAsync(br.TenantId, br.TopicId, ctx.Token).ConfigureAwait(false))?.Name;
            detail.Audio = (await Server.Database.MediaAssets.EnumerateAsync(new MediaAssetFilter { TenantId = br.TenantId, BriefingId = br.Id, PageSize = 20 }, ctx.Token).ConfigureAwait(false)).Objects
                .OrderByDescending(m => m.CreatedUtc).FirstOrDefault();
            if (detail.Audio != null) detail.Audio.ScriptMarkdown = null;
            foreach (BriefingItem item in detail.Items)
            {
                if (detail.ItemTitles.ContainsKey(item.ItemId)) continue;
                string? title = item.ItemType == BriefingItemTypeEnum.Finding ? (await Server.Database.Findings.ReadAsync(br.TenantId, item.ItemId, ctx.Token).ConfigureAwait(false))?.Title
                    : item.ItemType == BriefingItemTypeEnum.Prediction ? (await Server.Database.Predictions.ReadAsync(br.TenantId, item.ItemId, ctx.Token).ConfigureAwait(false))?.Statement
                    : (await Server.Database.Suggestions.ReadAsync(br.TenantId, item.ItemId, ctx.Token).ConfigureAwait(false))?.Title;
                if (title != null) detail.ItemTitles[item.ItemId] = title;
            }
            await SendJsonAsync(ctx, 200, detail).ConfigureAwait(false);
        }

        private async Task DownloadAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false);
            if (br == null) return;
            string format = (Query(ctx, "format") ?? "md").Trim().ToLowerInvariant();
            if (format != "md" && format != "markdown" && format != "html")
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "format must be md or html.").ConfigureAwait(false);
                return;
            }

            string? artifactId = format == "html" ? br.HtmlArtifactId : br.MarkdownArtifactId;
            Artifact? art = artifactId == null ? null : await Server.Database.Artifacts.ReadAsync(br.TenantId, artifactId, ctx.Token).ConfigureAwait(false);
            if (art == null)
            {
                await SendErrorAsync(ctx, br.Status == BriefingStatusEnum.Ready ? 410 : 409, br.Status == BriefingStatusEnum.Ready ? "ArtifactExpired" : "Conflict",
                    br.Status == BriefingStatusEnum.Ready ? "The report file is not available. Regenerate it to restore the file." : "The briefing is not ready yet.").ConfigureAwait(false);
                return;
            }

            await SendArtifactAsync(ctx, Server.Artifacts, art, true).ConfigureAwait(false);
        }

        private async Task ProgressAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false);
            if (br == null) return;
            string? userId = Context(ctx).UserId;
            if (String.IsNullOrEmpty(userId)) { await SendErrorAsync(ctx, 400, "BadRequest", "Listening progress belongs to a signed-in user.").ConfigureAwait(false); return; }
            BriefingProgress? p = await FindProgressAsync(br, userId, ctx).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, p ?? new BriefingProgress { TenantId = br.TenantId, UserId = userId, BriefingId = br.Id }).ConfigureAwait(false);
        }

        private async Task SaveProgressAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingRead).ConfigureAwait(false);
            if (br == null) return;
            string? userId = Context(ctx).UserId;
            if (String.IsNullOrEmpty(userId)) { await SendErrorAsync(ctx, 400, "BadRequest", "Listening progress belongs to a signed-in user.").ConfigureAwait(false); return; }
            BriefingProgressRequest? req = await ReadBodyAsync<BriefingProgressRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (Double.IsNaN(req.PositionSeconds) || req.PositionSeconds < 0) { await SendErrorAsync(ctx, 400, "ValidationFailed", "positionSeconds must be zero or more.").ConfigureAwait(false); return; }
            BriefingProgress? p = await FindProgressAsync(br, userId, ctx).ConfigureAwait(false);
            if (p == null)
            {
                p = new BriefingProgress { TenantId = br.TenantId, UserId = userId, BriefingId = br.Id, PositionSeconds = req.PositionSeconds, DurationSeconds = req.DurationSeconds, Completed = req.Completed };
                try
                {
                    p = await Server.Database.BriefingProgress.CreateAsync(p, ctx.Token).ConfigureAwait(false);
                }
                catch (Npgsql.PostgresException pe) when (pe.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
                {
                    p = (await FindProgressAsync(br, userId, ctx).ConfigureAwait(false))!;
                }
            }

            p.PositionSeconds = req.PositionSeconds;
            if (req.DurationSeconds.HasValue) p.DurationSeconds = req.DurationSeconds;
            p.Completed = req.Completed;
            p = await Server.Database.BriefingProgress.UpdateAsync(p, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, p).ConfigureAwait(false);
        }

        private async Task<BriefingProgress?> FindProgressAsync(Briefing br, string userId, HttpContextBase ctx)
        {
            return (await Server.Database.BriefingProgress.EnumerateAsync(new BriefingProgressFilter { TenantId = br.TenantId, UserId = userId, BriefingId = br.Id, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
        }

        private async Task PinAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingWrite).ConfigureAwait(false);
            if (br == null) return;
            BriefingPinRequest? req = await ReadBodyAsync<BriefingPinRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Briefing updated = await Server.Artifacts.SetBriefingPinnedAsync(br, req.Pinned, ctx.Token).ConfigureAwait(false);
            updated.Markdown = null;
            await SendJsonAsync(ctx, 200, updated).ConfigureAwait(false);
        }

        private async Task RegenerateAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingWrite).ConfigureAwait(false);
            if (br == null) return;
            Briefing updated = await Server.Briefings.RegenerateFilesAsync(br, ctx.Token).ConfigureAwait(false);
            updated.Markdown = null;
            await SendJsonAsync(ctx, 200, updated).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            Briefing? br = await OwnedAsync(ctx, RequestTypeEnum.BriefingWrite).ConfigureAwait(false);
            if (br == null) return;
            await Server.Briefings.DeleteBriefingAsync(br, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ListArtifactsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ArtifactRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            ArtifactFilter filter = new ArtifactFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                BriefingId = Query(ctx, "briefingId"),
                Kind = QueryEnum<ArtifactKindEnum>(ctx, "kind"),
                Pinned = QueryBool(ctx, "pinned")
            };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Artifacts.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadArtifactAsync(HttpContextBase ctx)
        {
            Artifact? art = await OwnedArtifactAsync(ctx).ConfigureAwait(false);
            if (art == null) return;
            await SendJsonAsync(ctx, 200, art).ConfigureAwait(false);
        }

        private async Task ContentAsync(HttpContextBase ctx)
        {
            Artifact? art = await OwnedArtifactAsync(ctx).ConfigureAwait(false);
            if (art == null) return;
            await SendArtifactAsync(ctx, Server.Artifacts, art, QueryBool(ctx, "download") == true).ConfigureAwait(false);
        }

        private async Task DownloadUrlAsync(HttpContextBase ctx)
        {
            Artifact? art = await OwnedArtifactAsync(ctx).ConfigureAwait(false);
            if (art == null) return;
            if (art.DeletedUtc.HasValue) throw new MyMorningNews.Core.Exceptions.ArtifactExpiredException("This file passed the retention window.");
            string? url = await Server.Artifacts.DownloadUrlAsync(art, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new DownloadUrlResponse
            {
                Url = url,
                ExpiresUtc = url == null ? null : DateTime.UtcNow.AddMinutes(Server.Settings.Storage.PresignedUrlMinutes),
                ContentPath = "/v1.0/api/artifacts/" + art.Id + "/content"
            }).ConfigureAwait(false);
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

        private async Task<Artifact?> OwnedArtifactAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.ArtifactRead).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Artifact? art = await Server.Database.Artifacts.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (art == null || !IsOwnerOrAdmin(ctx, art.UserId))
            {
                await SendNotFoundAsync(ctx, "File").ConfigureAwait(false);
                return null;
            }

            return art;
        }
    }
}
