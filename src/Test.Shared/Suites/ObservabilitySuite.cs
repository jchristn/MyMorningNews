namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Health, OpenAPI, CORS, request history, audit, and settings routes.
    /// </summary>
    public static class ObservabilitySuite
    {
        private const string S = "Observability";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Health, OpenAPI, request history, audit, and settings", new[]
            {
                Cases.Case(S, "Health", "Health reports Healthy with the database component", HealthAsync, "smoke", "fast"),
                Cases.Case(S, "OpenApi", "OpenAPI documents the control-plane and observability routes", OpenApiAsync, "smoke"),
                Cases.Case(S, "CorsPreflight", "OPTIONS preflight returns CORS headers", CorsAsync),
                Cases.Case(S, "RequestIdHeader", "Responses carry X-Request-Id", RequestIdAsync),
                Cases.Case(S, "HistoryCapturesAndRedacts", "Requests are captured with secrets redacted", HistoryAsync),
                Cases.Case(S, "HistorySummaryGapFill", "Summaries include every bucket in the range", SummaryAsync),
                Cases.Case(S, "HistoryTenantScoped", "Members see only their tenant's requests", HistoryScopedAsync),
                Cases.Case(S, "SettingsMaskedAndUpdated", "Settings mask secrets and apply live updates", SettingsAsync),
                Cases.Case(S, "ServerInfo", "Server info lists bundled tools", ServerInfoAsync)
            });
        }

        private static async Task HealthAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            HealthResponse h = (await anon.GetAsync("/v1.0/api/health", token).ConfigureAwait(false)).Expect(200, "health").Json<HealthResponse>();
            Assert.Equal("Healthy", h.Status, "status");
            Assert.Equal("Healthy", h.Components["database"], "database component");
        }

        private static async Task OpenApiAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            TestResponse r = (await anon.GetAsync("/openapi.json", token).ConfigureAwait(false)).Expect(200, "openapi");
            using (JsonDocument doc = JsonDocument.Parse(r.Body))
            {
                JsonElement paths = doc.RootElement.GetProperty("paths");
                foreach (string p in new[] { "/v1.0/token", "/v1.0/tenants", "/v1.0/api/request-history", "/v1.0/api/request-history/summary", "/v1.0/api/settings", "/v1.0/api/audit", "/v1.0/api/health" })
                    Assert.True(paths.TryGetProperty(p, out JsonElement _), "openapi documents " + p);

                JsonElement create = paths.GetProperty("/v1.0/api/vault/credentials").GetProperty("post");
                string schemaRef = create.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString() ?? String.Empty;
                string component = schemaRef.Substring(schemaRef.LastIndexOf('/') + 1);
                JsonElement schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
                Assert.True(schemas.GetProperty(component).GetProperty("properties").TryGetProperty("secret", out JsonElement _), "request body schema resolves with camelCase properties");
                List<string> listParams = paths.GetProperty("/v1.0/api/audit").GetProperty("get").GetProperty("parameters").EnumerateArray().Select(e => e.GetProperty("name").GetString() ?? String.Empty).ToList();
                Assert.True(listParams.Contains("pageSize") && listParams.Contains("sortDirection") && listParams.Contains("eventType"), "list routes document paging and filters: " + String.Join(",", listParams));
                List<string> historyParams = paths.GetProperty("/v1.0/api/request-history").GetProperty("get").GetProperty("parameters").EnumerateArray().Select(e => e.GetProperty("name").GetString() ?? String.Empty).ToList();
                Assert.True(historyParams.Contains("statusClass") && historyParams.Contains("minStatusCode"), "status range documented");
            }

            using (HttpClient http = new HttpClient())
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, anon.BaseUrl + "/openapi.json"))
            {
                req.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1:12101");
                using (HttpResponseMessage resp = await http.SendAsync(req, token).ConfigureAwait(false))
                {
                    List<string> origins = resp.Headers.TryGetValues("Access-Control-Allow-Origin", out IEnumerable<string>? values) ? values.ToList() : new List<string>();
                    Assert.True(origins.Count == 1 && origins[0] == "*", "exactly one allow-origin value: " + String.Join(" | ", origins));
                }
            }
        }

        private static async Task CorsAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            using (HttpClient http = new HttpClient())
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Options, anon.BaseUrl + "/v1.0/tenants"))
            using (HttpResponseMessage resp = await http.SendAsync(req, token).ConfigureAwait(false))
            {
                Assert.Equal(200, (int)resp.StatusCode, "preflight status");
                Assert.True(resp.Headers.Contains("Access-Control-Allow-Origin"), "allow-origin header");
            }
        }

        private static async Task RequestIdAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            using (HttpClient http = new HttpClient())
            using (HttpResponseMessage resp = await http.GetAsync(anon.BaseUrl + "/v1.0/tenants", token).ConfigureAwait(false))
            {
                Assert.True(resp.Headers.TryGetValues("X-Request-Id", out IEnumerable<string>? v) && v.First().StartsWith("rqi_", StringComparison.Ordinal), "request id header");
                Assert.True(resp.Headers.Contains("Access-Control-Allow-Origin"), "CORS header on normal responses");
            }
        }

        private static async Task<EnumerationResult<RequestHistoryEntry>> WaitForHistoryAsync(TestApiClient admin, string query, Func<EnumerationResult<RequestHistoryEntry>, bool> done, CancellationToken token)
        {
            EnumerationResult<RequestHistoryEntry> last = new EnumerationResult<RequestHistoryEntry>();
            for (int i = 0; i < 40; i++)
            {
                last = (await admin.GetAsync("/v1.0/api/request-history?" + query, token).ConfigureAwait(false)).Expect(200, "history").Json<EnumerationResult<RequestHistoryEntry>>();
                if (done(last)) return last;
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            return last;
        }

        private static async Task HistoryAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            string marker = "marker" + Guid.NewGuid().ToString("N").Substring(0, 8);
            (await admin.GetAsync("/v1.0/tenants/default/users?search=" + marker, token).ConfigureAwait(false)).Expect(200, "marked request");
            EnumerationResult<RequestHistoryEntry> page = await WaitForHistoryAsync(admin, "pathContains=/users&pageSize=50", p => p.Items.Any(i => i.Url.Contains(marker, StringComparison.Ordinal)), token).ConfigureAwait(false);
            RequestHistoryEntry? summary = page.Items.FirstOrDefault(i => i.Url.Contains(marker, StringComparison.Ordinal));
            Assert.NotNull(summary, "captured request");
            Assert.True(summary!.RequestBody == null && summary.ResponseBody == null, "list omits bodies");
            RequestHistoryEntry full = (await admin.GetAsync("/v1.0/api/request-history/" + summary.Id, token).ConfigureAwait(false)).Expect(200, "read entry").Json<RequestHistoryEntry>();
            Assert.Equal("[redacted]", full.RequestHeaders.First(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value, "authorization redacted");
            Assert.True(full.ResponseBody != null && full.ResponseBody.Contains("items", StringComparison.Ordinal), "response body captured");
            Assert.Equal("/v1.0/tenants/{tenantId}/users", full.RouteTemplate, "route template");
            (await admin.DeleteAsync("/v1.0/api/request-history/" + summary.Id, token).ConfigureAwait(false)).Expect(204, "delete entry");
        }

        private static async Task SummaryAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            DateTime to = DateTime.UtcNow.AddMinutes(1);
            DateTime from = to.AddHours(-1);
            string q = "fromUtc=" + Uri.EscapeDataString(from.ToString("o")) + "&toUtc=" + Uri.EscapeDataString(to.ToString("o")) + "&bucketMinutes=5";
            RequestHistorySummary s = (await admin.GetAsync("/v1.0/api/request-history/summary?" + q, token).ConfigureAwait(false)).Expect(200, "summary").Json<RequestHistorySummary>();
            Assert.Equal(12, s.Buckets.Count, "12 five-minute buckets");
            Assert.Equal(s.TotalCount, s.Buckets.Sum(b => b.SuccessCount + b.FailureCount), "totals match buckets");
            (await admin.GetAsync("/v1.0/api/request-history/summary", token).ConfigureAwait(false)).Expect(400, "missing range");
            await admin.GetAsync("/v1.0/api/tenants-does-not-exist", token).ConfigureAwait(false);
            EnumerationResult<RequestHistoryEntry> failures = (await admin.GetAsync("/v1.0/api/request-history?statusClass=4xx&pageSize=100", token).ConfigureAwait(false)).Expect(200, "4xx").Json<EnumerationResult<RequestHistoryEntry>>();
            Assert.True(failures.Items.Count > 0 && failures.Items.All(e => e.StatusCode >= 400 && e.StatusCode <= 499), "status class filter");
            EnumerationResult<RequestHistoryEntry> ok = (await admin.GetAsync("/v1.0/api/request-history?maxStatusCode=299&pageSize=100", token).ConfigureAwait(false)).Expect(200, "2xx").Json<EnumerationResult<RequestHistoryEntry>>();
            Assert.True(ok.Items.All(e => e.StatusCode <= 299), "max status filter");
        }

        private static async Task HistoryScopedAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            await ta.Client.GetAsync("/v1.0/api/me", token).ConfigureAwait(false);
            EnumerationResult<RequestHistoryEntry> page = await WaitForHistoryAsync(ta.Client, "pageSize=100", p => p.TotalCount > 0, token).ConfigureAwait(false);
            Assert.True(page.Items.All(i => i.TenantId == t.Id), "only own tenant");
        }

        private static async Task SettingsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            SettingsResponse s = (await admin.GetAsync("/v1.0/api/settings", token).ConfigureAwait(false)).Expect(200, "settings").Json<SettingsResponse>();
            Assert.Equal("********", s.Settings.Auth.SigningKey, "signing key masked");
            Assert.True(s.Fields.Any(f => f.Path == "rest.port" && f.RestartRequired), "port marked restart required");
            ServerSettings next = s.Settings;
            next.RequestHistory.RetentionDays = s.Settings.RequestHistory.RetentionDays == 30 ? 31 : 30;
            SettingsUpdateResult r = (await admin.PutAsync("/v1.0/api/settings", next, token).ConfigureAwait(false)).Expect(200, "update").Json<SettingsUpdateResult>();
            Assert.True(r.AppliedLive.Contains("requestHistory.retentionDays"), "applied live");
            TestResponse again = (await admin.GetAsync("/v1.0/api/settings", token).ConfigureAwait(false)).Expect(200, "reread");
            Assert.Equal(next.RequestHistory.RetentionDays, again.Json<SettingsResponse>().Settings.RequestHistory.RetentionDays, "value persisted");
            (await admin.PutAsync("/v1.0/api/settings", "{\"logging\":{\"logDirectory\":\" \"}}", token).ConfigureAwait(false)).Expect(400, "invalid value rejected");
            (await admin.PutAsync("/v1.0/api/settings", "{\"rest\":{\"port\":\"abc\"}}", token).ConfigureAwait(false)).Expect(400, "wrong type rejected");
        }

        private static async Task ServerInfoAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            ServerInfoResponse info = (await admin.GetAsync("/v1.0/api/server-info", token).ConfigureAwait(false)).Expect(200, "server info").Json<ServerInfoResponse>();
            Assert.True(info.ExternalServices.Any(e => e.Name == "Grafana" && e.Url.StartsWith("http://127.0.0.1", StringComparison.Ordinal)), "grafana link on loopback");
            Assert.True(info.SchemaVersion >= 1 && info.DatabaseType == "Postgresql", "database info");
        }
    }
}
