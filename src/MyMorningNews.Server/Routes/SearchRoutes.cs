namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Search;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Search providers (plan section 15), per-topic provider choices, and a fetch test.
    /// </summary>
    public class SearchRoutes : RouteBase
    {
        private const string B = "/v1.0/api/search-providers";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public SearchRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, ListAsync, null, openApiMetadata: Paged(Doc("List search providers", "Search").WithParameter(QueryParam("kind", typeof(SearchProviderKindEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B, CreateAsync, null, openApiMetadata: Doc("Add a search provider", "Search").WithRequestBody(Body<SearchProviderUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a search provider", "Search"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update a search provider", "Search").WithRequestBody(Body<SearchProviderUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a search provider", "Search"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/test", TestAsync, null, openApiMetadata: Doc("Run a test query", "Search").WithRequestBody(Body<SearchTestRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/topics/{id}/search-providers", TopicProvidersAsync, null, openApiMetadata: Doc("Search providers a topic uses", "Search"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/topics/{id}/search-providers", SetTopicProvidersAsync, null, openApiMetadata: Doc("Choose a topic's search providers (empty uses the tenant defaults)", "Search")
                .WithRequestBody(Body<List<TopicSearchProviderRequest>>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/fetch/test", FetchTestAsync, null, openApiMetadata: Doc("Fetch a page as the research pipeline would", "Search").WithRequestBody(Body<FetchTestRequest>()));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SearchProviderRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            SearchProviderFilter filter = new SearchProviderFilter { TenantId = tenantId, Kind = QueryEnum<SearchProviderKindEnum>(ctx, "kind") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.SearchProviders.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SearchProviderWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            SearchProviderUpsertRequest? req = await ReadBodyAsync<SearchProviderUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name) || !req.Kind.HasValue)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "name and kind are required.").ConfigureAwait(false);
                return;
            }

            SearchProvider p = new SearchProvider { TenantId = tenantId };
            string? error = await ApplyAsync(p, req, ctx).ConfigureAwait(false);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            p = await Server.Database.SearchProviders.CreateAsync(p, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, p).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            SearchProvider? p = await ProviderAsync(ctx, RequestTypeEnum.SearchProviderRead).ConfigureAwait(false);
            if (p == null) return;
            await SendJsonAsync(ctx, 200, p).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            SearchProvider? p = await ProviderAsync(ctx, RequestTypeEnum.SearchProviderWrite).ConfigureAwait(false);
            if (p == null) return;
            SearchProviderUpsertRequest? req = await ReadBodyAsync<SearchProviderUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string? error = await ApplyAsync(p, req, ctx).ConfigureAwait(false);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            p = await Server.Database.SearchProviders.UpdateAsync(p, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, p).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            SearchProvider? p = await ProviderAsync(ctx, RequestTypeEnum.SearchProviderWrite).ConfigureAwait(false);
            if (p == null) return;
            await Server.Database.SearchProviders.DeleteAsync(p.TenantId, p.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task TestAsync(HttpContextBase ctx)
        {
            SearchProvider? p = await ProviderAsync(ctx, RequestTypeEnum.SearchProviderRead).ConfigureAwait(false);
            if (p == null) return;
            SearchTestRequest req = new SearchTestRequest();
            if (!String.IsNullOrWhiteSpace(ctx.Request.DataAsString))
            {
                SearchTestRequest? parsed = await ReadBodyAsync<SearchTestRequest>(ctx).ConfigureAwait(false);
                if (parsed == null) return;
                req = parsed;
            }

            SearchOutcome outcome = await Server.Search.SearchAsync(p, new ResearchSearchRequest { Query = req.Query, MaxResults = req.MaxResults, FeedUrls = req.FeedUrls ?? new List<string>() }, ctx.Token).ConfigureAwait(false);
            p.LastTestedUtc = DateTime.UtcNow;
            p.LastTestOk = outcome.Success;
            await Server.Database.SearchProviders.UpdateAsync(p, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, outcome).ConfigureAwait(false);
        }

        private async Task TopicProvidersAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            Topic? topic = await TopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            await SendJsonAsync(ctx, 200, await Server.Search.ProvidersForTopicAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SetTopicProvidersAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await TopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            List<TopicSearchProviderRequest>? req = await ReadBodyAsync<List<TopicSearchProviderRequest>>(ctx).ConfigureAwait(false);
            if (req == null) return;
            HashSet<string> valid = new HashSet<string>((await Server.Database.SearchProviders.EnumerateAsync(new SearchProviderFilter { TenantId = topic.TenantId, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Items.Select(p => p.Id), StringComparer.Ordinal);
            if (req.Any(r => !valid.Contains(r.SearchProviderId)))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Every searchProviderId must be one of this tenant's providers.").ConfigureAwait(false);
                return;
            }

            foreach (TopicSearchProvider existing in (await Server.Database.TopicSearchProviders.EnumerateAsync(new TopicSearchProviderFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Items)
                await Server.Database.TopicSearchProviders.DeleteAsync(topic.TenantId, existing.Id, ctx.Token).ConfigureAwait(false);
            foreach (TopicSearchProviderRequest r in req.GroupBy(r => r.SearchProviderId).Select(g => g.First()))
            {
                await Server.Database.TopicSearchProviders.CreateAsync(new TopicSearchProvider
                {
                    TenantId = topic.TenantId, TopicId = topic.Id, SearchProviderId = r.SearchProviderId, Enabled = r.Enabled,
                    MaxResults = r.MaxResults.HasValue ? Math.Clamp(r.MaxResults.Value, 1, 50) : null
                }, ctx.Token).ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 200, await Server.Search.ProvidersForTopicAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task FetchTestAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SearchProviderWrite).ConfigureAwait(false)) return;
            FetchTestRequest? req = await ReadBodyAsync<FetchTestRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            FetchResult result = await Server.Fetch.FetchAsync(req.Url, null, ctx.Token).ConfigureAwait(false);
            if (result.Text != null && result.Text.Length > 20000) result.Text = result.Text.Substring(0, 20000);
            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private async Task<SearchProvider?> ProviderAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            SearchProvider? p = await Server.Database.SearchProviders.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (p == null) await SendNotFoundAsync(ctx, "Search provider").ConfigureAwait(false);
            return p;
        }

        private async Task<Topic?> TopicAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId))
            {
                await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false);
                return null;
            }

            return topic;
        }

        private async Task<string?> ApplyAsync(SearchProvider p, SearchProviderUpsertRequest req, HttpContextBase ctx)
        {
            if (!String.IsNullOrWhiteSpace(req.Name)) p.Name = req.Name.Trim().Length > 128 ? req.Name.Trim().Substring(0, 128) : req.Name.Trim();
            if (req.Kind.HasValue)
            {
                if (req.Kind.Value == SearchProviderKindEnum.HostedModelWebSearch) return "Hosted model web search is configured on OpenAI, Azure, or xAI model endpoints, not as a search provider.";
                p.Kind = req.Kind.Value;
            }

            if (req.Endpoint != null)
            {
                string e = req.Endpoint.Trim();
                if (e.Length > 0 && !(Uri.TryCreate(e, UriKind.Absolute, out Uri? u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))) return "endpoint must be an absolute http or https URL.";
                p.Endpoint = e.Length == 0 ? null : e;
            }

            if (req.CredentialId != null)
            {
                if (req.CredentialId.Length == 0) p.CredentialId = null;
                else if (await Server.Database.VaultCredentials.ReadAsync(p.TenantId, req.CredentialId, ctx.Token).ConfigureAwait(false) == null) return "credentialId must be a stored credential in this tenant.";
                else p.CredentialId = req.CredentialId;
            }

            if (!String.IsNullOrEmpty(req.ApiKey))
            {
                VaultCredential created = await Server.Vault.CreateAsync(p.TenantId, Context(ctx).UserId, new VaultCredentialUpsertRequest
                {
                    Name = p.Name + " key " + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                    Kind = VaultCredentialKindEnum.ApiKey,
                    Provider = p.Kind == SearchProviderKindEnum.Tavily ? VaultProviderEnum.Tavily : p.Kind == SearchProviderKindEnum.YouCom ? VaultProviderEnum.YouCom : VaultProviderEnum.Custom,
                    Secret = req.ApiKey
                }, ctx.Token).ConfigureAwait(false);
                p.CredentialId = created.Id;
            }

            if (req.IsDefault.HasValue) p.IsDefault = req.IsDefault.Value;
            if (req.MaxResults.HasValue) p.MaxResults = req.MaxResults.Value;
            if (req.TimeoutMs.HasValue) p.TimeoutMs = req.TimeoutMs.Value;
            if (req.Active.HasValue) p.Active = req.Active.Value;
            if ((p.Kind == SearchProviderKindEnum.Tavily || p.Kind == SearchProviderKindEnum.YouCom) && String.IsNullOrEmpty(p.CredentialId))
                return p.Kind + " needs an API key: send apiKey or a credentialId.";
            return null;
        }
    }
}
