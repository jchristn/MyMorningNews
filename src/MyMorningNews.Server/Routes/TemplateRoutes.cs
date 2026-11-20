namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Briefing templates (plan section 21.1): shared tenant templates (seeded defaults; changed by tenant administrators)
    /// and personal templates, with versions, validation, rollback, reset, import (create from Markdown), and export.
    /// </summary>
    public class TemplateRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public TemplateRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/briefing-templates";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List briefing templates I can use", "Templates", "Shared tenant templates plus my own.").WithParameter(QueryParam("userId", typeof(string)))
                .WithParameter(QueryParam("scope", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b, CreateAsync, null, openApiMetadata: Doc("Create (import) a template from Markdown", "Templates").WithRequestBody(Body<TemplateCreateRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/validate", ValidateAsync, null, openApiMetadata: Doc("Validate template Markdown without saving", "Templates")
                .WithRequestBody(Body<TemplateValidateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a template with its current content", "Templates"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, b + "/{id}", SaveAsync, null, openApiMetadata: Doc("Save a new template version", "Templates").WithRequestBody(Body<TemplateSaveRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a template", "Templates"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/versions", VersionsAsync, null, openApiMetadata: Doc("List template versions", "Templates"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/rollback", RollbackAsync, null, openApiMetadata: Doc("Restore an earlier version", "Templates")
                .WithRequestBody(Body<TemplateRollbackRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/reset", ResetAsync, null, openApiMetadata: Doc("Reset a seeded template to the shipped default", "Templates"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, b + "/{id}/export", ExportAsync, null, openApiMetadata: Doc("Export the current version as a Markdown file", "Templates"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TemplateRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Stopwatch sw = Stopwatch.StartNew();
            RequestContext rc = Context(ctx);
            BriefingTemplateFilter filter = new BriefingTemplateFilter { TenantId = tenantId, PageSize = 1000 };
            List<BriefingTemplate> all = (await Server.Database.BriefingTemplates.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).Items
                .Where(t => t.UserId == null || t.UserId == rc.UserId || ((rc.IsAdmin || rc.IsTenantAdmin) && Query(ctx, "userId") == t.UserId))
                .OrderBy(t => t.UserId == null ? 0 : 1).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
            string scope = (Query(ctx, "scope") ?? "all").Trim().ToLowerInvariant();
            if (scope == "shared") all = all.Where(t => t.UserId == null).ToList();
            else if (scope == "mine") all = all.Where(t => t.UserId != null).ToList();
            string? search = Query(ctx, "search");
            if (!String.IsNullOrWhiteSpace(search))
                all = all.Where(t => t.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) || (t.Description ?? String.Empty).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            EnumerationFilter paging = new EnumerationFilter();
            ApplyPaging(ctx, paging);
            List<BriefingTemplate> page = all.Skip((paging.PageNumber - 1) * paging.PageSize).Take(paging.PageSize).ToList();
            await SendJsonAsync(ctx, 200, new EnumerationResult<BriefingTemplate>
            {
                Items = page,
                PageNumber = paging.PageNumber,
                PageSize = paging.PageSize,
                TotalCount = all.Count,
                TotalPages = all.Count == 0 ? 0 : (int)Math.Ceiling(all.Count / (double)paging.PageSize),
                DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            }).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TemplateWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            TemplateCreateRequest? req = await ReadBodyAsync<TemplateCreateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            RequestContext rc = Context(ctx);
            if (req.Shared && !(rc.IsAdmin || rc.IsTenantAdmin))
            {
                await SendErrorAsync(ctx, 403, "Forbidden", "Only tenant administrators can create shared templates.").ConfigureAwait(false);
                return;
            }

            BriefingTemplate t = await Server.Briefings.Templates.CreateAsync(tenantId, req.Shared ? null : rc.UserId, req.Content ?? String.Empty, req.Name, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await DescribeAsync(t, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ValidateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TemplateRead).ConfigureAwait(false)) return;
            TemplateValidateRequest? req = await ReadBodyAsync<TemplateValidateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            BriefingTemplateDefinition def = TemplateParser.Parse(req.Content);
            await SendJsonAsync(ctx, 200, new TemplateValidationResponse
            {
                Valid = !TemplateParser.HasErrors(def),
                Name = def.Name,
                Title = def.Title,
                Problems = def.Problems,
                Sections = def.Sections,
                Audio = def.Audio
            }).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateRead, false).ConfigureAwait(false);
            if (t == null) return;
            await SendJsonAsync(ctx, 200, await DescribeAsync(t, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SaveAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateWrite, true).ConfigureAwait(false);
            if (t == null) return;
            TemplateSaveRequest? req = await ReadBodyAsync<TemplateSaveRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await Server.Briefings.Templates.SaveAsync(t, req.Content ?? String.Empty, req.ChangeNote, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribeAsync(t, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateWrite, true).ConfigureAwait(false);
            if (t == null) return;
            await Server.Briefings.Templates.DeleteAsync(t, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task VersionsAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateRead, false).ConfigureAwait(false);
            if (t == null) return;
            await SendJsonAsync(ctx, 200, await Server.Briefings.Templates.VersionsAsync(t, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RollbackAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateWrite, true).ConfigureAwait(false);
            if (t == null) return;
            TemplateRollbackRequest? req = await ReadBodyAsync<TemplateRollbackRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await Server.Briefings.Templates.RollbackAsync(t, req.Version, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribeAsync(t, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ResetAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateWrite, true).ConfigureAwait(false);
            if (t == null) return;
            await Server.Briefings.Templates.ResetAsync(t, Context(ctx).UserId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribeAsync(t, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ExportAsync(HttpContextBase ctx)
        {
            BriefingTemplate? t = await VisibleAsync(ctx, RequestTypeEnum.TemplateRead, false).ConfigureAwait(false);
            if (t == null) return;
            BriefingTemplateVersion? v = await Server.Briefings.Templates.CurrentVersionAsync(t, ctx.Token).ConfigureAwait(false);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/markdown; charset=utf-8";
            ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=\"" + TemplateParser.Slug(t.Name) + ".md\"");
            await ctx.Response.Send(Encoding.UTF8.GetBytes(v?.Content ?? String.Empty), ctx.Token).ConfigureAwait(false);
        }

        private async Task<TemplateDetailResponse> DescribeAsync(BriefingTemplate t, HttpContextBase ctx)
        {
            BriefingTemplateVersion? v = await Server.Briefings.Templates.CurrentVersionAsync(t, ctx.Token).ConfigureAwait(false);
            BriefingTemplateDefinition def = TemplateParser.Parse(v?.Content);
            return new TemplateDetailResponse { Template = t, Content = v?.Content ?? String.Empty, Problems = def.Problems, Sections = def.Sections, Shared = t.UserId == null, Title = def.Title, Audio = def.Audio };
        }

        private async Task<BriefingTemplate?> VisibleAsync(HttpContextBase ctx, RequestTypeEnum type, bool write)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            RequestContext rc = Context(ctx);
            BriefingTemplate? t = await Server.Database.BriefingTemplates.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            bool admin = rc.IsAdmin || rc.IsTenantAdmin;
            if (t == null || (t.UserId != null && t.UserId != rc.UserId && !admin))
            {
                await SendNotFoundAsync(ctx, "Template").ConfigureAwait(false);
                return null;
            }

            if (write && t.UserId == null && !admin)
            {
                await SendErrorAsync(ctx, 403, "Forbidden", "Only tenant administrators can change shared templates. Create your own copy instead.").ConfigureAwait(false);
                return null;
            }

            return t;
        }
    }
}
