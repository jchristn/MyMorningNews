namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Search;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Search providers (SearXNG, Tavily, You.com, RSS, Hacker News, arXiv), URL canonicalization, and page fetching,
    /// against the stub server.
    /// </summary>
    public static class SearchSuite
    {
        private const string S = "Search";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Search and fetch", new[]
            {
                Cases.Case(S, "Canonical", "URLs are canonicalized and hashed for de-duplication", CanonicalAsync, "fast"),
                Cases.Case(S, "FeedParser", "RSS and Atom items parse with dates, links, and plain-text summaries", FeedParserAsync, "fast"),
                Cases.Case(S, "Defaults", "New tenants get SearXNG and RSS by default plus Hacker News and arXiv", DefaultsAsync),
                Cases.Case(S, "Searxng", "SearXNG results are de-duplicated, domain-filtered, and dated", SearxngAsync),
                Cases.Case(S, "KeylessSources", "Hacker News, arXiv, and topic feeds return matching items", KeylessAsync),
                Cases.Case(S, "KeyedSources", "Tavily and You.com use vault keys and report bad keys as errors", KeyedAsync),
                Cases.Case(S, "TopicProviders", "Topics choose providers or fall back to the tenant defaults", TopicProvidersAsync),
                Cases.Case(S, "Fetch", "Pages are fetched and converted; robots, blocks, errors, and types are reported", FetchAsync)
            });
        }

        private static Task CanonicalAsync(CancellationToken token)
        {
            Assert.Equal("https://example.com/a/b?x=1&y=2", UrlCanonicalizer.Canonicalize("HTTPS://Example.COM:443/a/b/?y=2&utm_source=n&x=1&fbclid=z#top"), "canonical form");
            Assert.Equal("http://example.com:8080/", UrlCanonicalizer.Canonicalize("http://example.com:8080"), "root keeps its slash and custom port");
            Assert.True(UrlCanonicalizer.Canonicalize("ftp://example.com/x") == null && UrlCanonicalizer.Canonicalize("not a url") == null, "rejects non-web URLs");
            Assert.Equal(UrlCanonicalizer.Hash("https://www.example.com/story?id=7&utm_medium=x"), UrlCanonicalizer.Hash("http://example.com/story/?id=7"), "hash ignores scheme, www, and tracking");
            Assert.True(UrlCanonicalizer.IsInDomain("https://news.blocked.test/x", "blocked.test") && !UrlCanonicalizer.IsInDomain("https://notblocked.test/x", "blocked.test"), "domain matching");
            return Task.CompletedTask;
        }

        private static Task FeedParserAsync(CancellationToken token)
        {
            string rss = "<rss version=\"2.0\"><channel><item><title>A &amp; B</title><link>https://a.example.com/1</link><description>&lt;p&gt;Hello &lt;b&gt;world&lt;/b&gt;&lt;/p&gt;</description><pubDate>Wed, 18 Nov 2026 08:00:00 GMT</pubDate></item></channel></rss>";
            List<SearchHit> hits = FeedParser.Parse(rss, "Rss");
            Assert.True(hits.Count == 1 && hits[0].Title == "A & B" && hits[0].Snippet == "Hello world" && hits[0].PublishedUtc == new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc), "rss item");
            string atom = "<feed xmlns=\"http://www.w3.org/2005/Atom\"><entry><title>T</title><link rel=\"related\" href=\"https://x.example.com/pdf\"/><link rel=\"alternate\" href=\"https://x.example.com/abs\"/><updated>2026-11-17T10:00:00Z</updated></entry></feed>";
            List<SearchHit> entries = FeedParser.Parse(atom, "Atom");
            Assert.True(entries.Count == 1 && entries[0].Url == "https://x.example.com/abs" && entries[0].PublishedUtc.HasValue, "atom alternate link");
            Assert.Equal(0, FeedParser.Parse("<html>not a feed</html", "Rss").Count, "malformed input is empty");
            return Task.CompletedTask;
        }

        private static async Task<TestUser> AdminAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            return await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
        }

        private static async Task<SearchProvider> ProviderAsync(TestUser ta, string kind, string? endpoint, string? apiKey, CancellationToken token)
        {
            return (await ta.Client.PostAsync("/v1.0/api/search-providers", new { name = "qa " + kind, kind = kind, endpoint = endpoint, apiKey = apiKey }, token).ConfigureAwait(false))
                .Expect(201, "provider " + kind).Json<SearchProvider>();
        }

        private static async Task DefaultsAsync(CancellationToken token)
        {
            TestUser ta = await AdminAsync(token).ConfigureAwait(false);
            EnumerationResult<SearchProvider> list = (await ta.Client.GetAsync("/v1.0/api/search-providers", token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<SearchProvider>>();
            Assert.True(list.Items.Count == 4, "four providers seeded (" + list.Items.Count + ")");
            Assert.True(list.Items.Where(p => p.IsDefault).Select(p => p.Kind).OrderBy(k => k).SequenceEqual(new[] { SearchProviderKindEnum.Searxng, SearchProviderKindEnum.Rss }.OrderBy(k => k)), "SearXNG and RSS are defaults");
            TestUser member = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), list.Items[0].TenantId, false, token).ConfigureAwait(false);
            (await member.Client.GetAsync("/v1.0/api/search-providers", token).ConfigureAwait(false)).Expect(200, "members read providers");
            (await member.Client.PostAsync("/v1.0/api/search-providers", new { name = "x", kind = "Searxng" }, token).ConfigureAwait(false)).Expect(403, "members cannot add providers");
        }

        private static async Task SearxngAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestUser ta = await AdminAsync(token).ConfigureAwait(false);
            SearchProvider p = await ProviderAsync(ta, "Searxng", StubModelServer.Shared.BaseUrl, null, token).ConfigureAwait(false);
            SearchOutcome viaApi = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + p.Id + "/test", new { query = "heat pumps", maxResults = 10 }, token).ConfigureAwait(false))
                .Expect(200, "test").Json<SearchOutcome>();
            Assert.True(viaApi.Success && viaApi.Hits.Count == 3, "duplicate removed (" + viaApi.Hits.Count + ")");
            SearchHit first = viaApi.Hits[0];
            Assert.True(first.CanonicalUrl == "https://www.example.com/story?id=7" && first.PublishedUtc.HasValue && first.Score == 2.5 && first.Rank == 1, "canonical, dated, scored: " + first.CanonicalUrl);
            SearchOutcome filtered = await server.Context.Search.SearchAsync(p, new ResearchSearchRequest { Query = "x", ExcludeDomains = new List<string> { "blocked.test" } }, token).ConfigureAwait(false);
            Assert.True(filtered.Hits.Count == 2 && filtered.Hits.All(h => !h.Url.Contains("blocked", StringComparison.Ordinal)), "excluded domains removed");
            SearchProvider tested = (await ta.Client.GetAsync("/v1.0/api/search-providers/" + p.Id, token).ConfigureAwait(false)).Json<SearchProvider>();
            Assert.True(tested.LastTestOk == true && tested.LastTestedUtc.HasValue, "test recorded");
            SearchProvider down = await ProviderAsync(ta, "Searxng", "http://127.0.0.1:1", null, token).ConfigureAwait(false);
            SearchOutcome failed = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + down.Id + "/test", null, token).ConfigureAwait(false)).Expect(200, "unreachable").Json<SearchOutcome>();
            Assert.True(!failed.Success && failed.Error != null, "failures reported, not thrown");
        }

        private static async Task KeylessAsync(CancellationToken token)
        {
            TestUser ta = await AdminAsync(token).ConfigureAwait(false);
            string b = StubModelServer.Shared.BaseUrl;
            SearchProvider hn = await ProviderAsync(ta, "HackerNews", b + "/hn", null, token).ConfigureAwait(false);
            SearchOutcome h = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + hn.Id + "/test", new { query = "agents" }, token).ConfigureAwait(false)).Json<SearchOutcome>();
            Assert.True(h.Success && h.Hits.Count == 2 && h.Hits[1].Url == "https://news.ycombinator.com/item?id=102" && h.Hits[0].Score == 120, "hacker news: " + h.Error);
            SearchProvider ax = await ProviderAsync(ta, "Arxiv", b + "/arxiv", null, token).ConfigureAwait(false);
            SearchOutcome a = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + ax.Id + "/test", new { query = "agents" }, token).ConfigureAwait(false)).Json<SearchOutcome>();
            Assert.True(a.Success && a.Hits.Count == 1 && a.Hits[0].Url == "http://arxiv.org/abs/2611.00001v1" && a.Hits[0].Title == "Agentic Coding Benchmarks", "arxiv: " + a.Error);
            SearchProvider rss = await ProviderAsync(ta, "Rss", null, null, token).ConfigureAwait(false);
            SearchOutcome r = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + rss.Id + "/test", new { query = "heat pump rebates", feedUrls = new[] { b + "/feed.xml", b + "/missing.xml" } }, token).ConfigureAwait(false)).Json<SearchOutcome>();
            Assert.True(r.Success && r.Hits.Count == 1 && r.Hits[0].Url == "https://energy.example.com/rebates" && r.Hits[0].Snippet == "New rebates announced.", "feeds filtered by query; a dead feed is skipped: " + r.Error);
        }

        private static async Task KeyedAsync(CancellationToken token)
        {
            TestUser ta = await AdminAsync(token).ConfigureAwait(false);
            string b = StubModelServer.Shared.BaseUrl;
            (await ta.Client.PostAsync("/v1.0/api/search-providers", new { name = "no key", kind = "Tavily" }, token).ConfigureAwait(false)).Expect(400, "tavily needs a key");
            (await ta.Client.PostAsync("/v1.0/api/search-providers", new { name = "hosted", kind = "HostedModelWebSearch" }, token).ConfigureAwait(false)).Expect(400, "hosted search lives on model endpoints");
            SearchProvider tav = await ProviderAsync(ta, "Tavily", b + "/tavily/search", "tvly-test-key", token).ConfigureAwait(false);
            Assert.True(!String.IsNullOrEmpty(tav.CredentialId), "key stored in the vault");
            SearchOutcome t = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + tav.Id + "/test", new { query = "agents" }, token).ConfigureAwait(false)).Json<SearchOutcome>();
            Assert.True(t.Success && t.Hits.Count == 2 && t.Hits[0].CanonicalUrl == "https://news.example.com/a", "tavily: " + t.Error);
            SearchProvider bad = await ProviderAsync(ta, "Tavily", b + "/tavily/search", "wrong-key", token).ConfigureAwait(false);
            SearchOutcome denied = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + bad.Id + "/test", null, token).ConfigureAwait(false)).Json<SearchOutcome>();
            Assert.True(!denied.Success && denied.Error != null, "bad key reported");
            SearchProvider you = await ProviderAsync(ta, "YouCom", b + "/you/search", "ydc-test", token).ConfigureAwait(false);
            SearchOutcome y = (await ta.Client.PostAsync("/v1.0/api/search-providers/" + you.Id + "/test", null, token).ConfigureAwait(false)).Json<SearchOutcome>();
            Assert.True(y.Success && y.Hits.Count == 1 && y.Hits[0].Snippet == "From You.", "you.com: " + y.Error);
        }

        private static async Task TopicProvidersAsync(CancellationToken token)
        {
            TestUser ta = await AdminAsync(token).ConfigureAwait(false);
            Topic topic = (await ta.Client.PostAsync("/v1.0/api/topics", new { name = "Providers topic" }, token).ConfigureAwait(false)).Json<Topic>();
            List<SearchProvider> defaults = (await ta.Client.GetAsync("/v1.0/api/topics/" + topic.Id + "/search-providers", token).ConfigureAwait(false)).Expect(200, "defaults").Json<List<SearchProvider>>();
            Assert.Equal(2, defaults.Count, "defaults when nothing chosen");
            EnumerationResult<SearchProvider> all = (await ta.Client.GetAsync("/v1.0/api/search-providers", token).ConfigureAwait(false)).Json<EnumerationResult<SearchProvider>>();
            SearchProvider hn = all.Items.Single(p => p.Kind == SearchProviderKindEnum.HackerNews);
            List<SearchProvider> chosen = (await ta.Client.PutAsync("/v1.0/api/topics/" + topic.Id + "/search-providers", new[] { new { searchProviderId = hn.Id, enabled = true, maxResults = 3 } }, token).ConfigureAwait(false))
                .Expect(200, "choose").Json<List<SearchProvider>>();
            Assert.True(chosen.Count == 1 && chosen[0].Id == hn.Id && chosen[0].MaxResults == 3, "chosen provider with limit");
            (await ta.Client.PutAsync("/v1.0/api/topics/" + topic.Id + "/search-providers", new[] { new { searchProviderId = "spv_nope", enabled = true } }, token).ConfigureAwait(false)).Expect(400, "unknown provider");
            List<SearchProvider> reset = (await ta.Client.PutAsync("/v1.0/api/topics/" + topic.Id + "/search-providers", new object[0], token).ConfigureAwait(false)).Json<List<SearchProvider>>();
            Assert.Equal(2, reset.Count, "empty choice falls back to defaults");
        }

        private static async Task FetchAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            int delay = server.Context.SettingsService.Current.Search.PerDomainDelayMs;
            server.Context.SettingsService.Current.Search.PerDomainDelayMs = 0;
            try
            {
                string b = StubModelServer.Shared.BaseUrl;
                TestUser ta = await AdminAsync(token).ConfigureAwait(false);
                FetchResult page = (await ta.Client.PostAsync("/v1.0/api/fetch/test", new { url = b + "/news/rebates.html?utm_source=x" }, token).ConfigureAwait(false)).Expect(200, "fetch").Json<FetchResult>();
                Assert.True(page.Disposition == FetchDispositionEnum.Fetched && page.Title == "Heat pump rebates expand" && page.StatusCode == 200, "fetched: " + page.Disposition + " " + page.Error);
                Assert.True(page.Text != null && page.Text.Contains("new rebates", StringComparison.Ordinal) && page.Text.Contains("Applications open", StringComparison.Ordinal) && !page.Text.Contains("<p>", StringComparison.Ordinal) && !page.Text.Contains("Menu", StringComparison.Ordinal), "converted to text without navigation: " + page.Text);
                Assert.True(page.CanonicalUrl == b + "/news/rebates.html" && page.ContentHash != null && page.ContentHash.Length == 64, "canonical and hashed");

                FetchService fetch = server.Context.Fetch;
                Assert.Equal(FetchDispositionEnum.RobotsDenied, (await fetch.FetchAsync(b + "/private/secret", null, token).ConfigureAwait(false)).Disposition, "robots disallow");
                Assert.Equal(FetchDispositionEnum.Fetched, (await fetch.FetchAsync(b + "/private/open/page", null, token).ConfigureAwait(false)).Disposition, "longer allow wins");
                FetchResult missing = await fetch.FetchAsync(b + "/missing/page", null, token).ConfigureAwait(false);
                Assert.True(missing.Disposition == FetchDispositionEnum.HttpError && missing.StatusCode == 404, "http errors reported: " + missing.Disposition);
                Assert.Equal(FetchDispositionEnum.Unsupported, (await fetch.FetchAsync(b + "/file.bin", null, token).ConfigureAwait(false)).Disposition, "binary is unsupported");
                Assert.Equal(FetchDispositionEnum.Blocked, (await fetch.FetchAsync("https://news.blocked.test/a", new[] { "blocked.test" }, token).ConfigureAwait(false)).Disposition, "blocked domain");
                Assert.Equal(FetchDispositionEnum.Unsupported, (await fetch.FetchAsync("mailto:x@example.com", null, token).ConfigureAwait(false)).Disposition, "not a web URL");

                MyMorningNews.Core.Settings.ServerSettings own = new MyMorningNews.Core.Settings.ServerSettings();
                own.Search.PerDomainDelayMs = 400;
                using (FetchService spaced = new FetchService(own, server.Context.Logging))
                {
                    await spaced.FetchAsync(b + "/a.html", null, token).ConfigureAwait(false);
                    Stopwatch sw = Stopwatch.StartNew();
                    await spaced.FetchAsync(b + "/b.html", null, token).ConfigureAwait(false);
                    Assert.True(sw.ElapsedMilliseconds >= 300, "per-domain delay respected (" + sw.ElapsedMilliseconds + " ms)");
                }
            }
            finally
            {
                server.Context.SettingsService.Current.Search.PerDomainDelayMs = delay;
            }
        }
    }
}
