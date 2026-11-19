namespace Test.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;

    /// <summary>
    /// Minimal HTTP client for route tests. Uses 127.0.0.1.
    /// </summary>
    public class TestApiClient
    {
        #region Public-Members

        /// <summary>
        /// Base URL.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Bearer token sent on every request when set.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Extra headers sent on every request.
        /// </summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Private-Members

        private static readonly HttpClient _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="baseUrl">Base URL.</param>
        public TestApiClient(string baseUrl)
        {
            BaseUrl = baseUrl.TrimEnd('/');
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Log in with email and password, storing the token.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="email">Email.</param>
        /// <param name="password">Password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token response.</returns>
        public async Task<TokenResponse> LoginAsync(string tenantId, string email, string password, CancellationToken token)
        {
            TestApiClient anon = new TestApiClient(BaseUrl);
            anon.Headers["x-tenant-guid"] = tenantId;
            anon.Headers["x-email"] = email;
            anon.Headers["x-password"] = password;
            TestResponse r = await anon.SendAsync(HttpMethod.Post, "/v1.0/token", null, token).ConfigureAwait(false);
            r.Expect(200, "login " + email);
            TokenResponse t = r.Json<TokenResponse>();
            Token = t.Token;
            return t;
        }

        /// <summary>
        /// GET.
        /// </summary>
        /// <param name="path">Path and query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public Task<TestResponse> GetAsync(string path, CancellationToken token)
        {
            return SendAsync(HttpMethod.Get, path, null, token);
        }

        /// <summary>
        /// POST a JSON body.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <param name="body">Body object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public Task<TestResponse> PostAsync(string path, object? body, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, path, body, token);
        }

        /// <summary>
        /// PUT a JSON body.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <param name="body">Body object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public Task<TestResponse> PutAsync(string path, object? body, CancellationToken token)
        {
            return SendAsync(HttpMethod.Put, path, body, token);
        }

        /// <summary>
        /// DELETE.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public Task<TestResponse> DeleteAsync(string path, CancellationToken token)
        {
            return SendAsync(HttpMethod.Delete, path, null, token);
        }

        /// <summary>
        /// Send a request.
        /// </summary>
        /// <param name="method">Method.</param>
        /// <param name="path">Path and query.</param>
        /// <param name="body">Body object, serialized as JSON; a string is sent verbatim.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        public async Task<TestResponse> SendAsync(HttpMethod method, string path, object? body, CancellationToken token)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(method, BaseUrl + path))
            {
                if (Token != null) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
                foreach (KeyValuePair<string, string> h in Headers) req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                if (body != null)
                {
                    string json = body as string ?? Serializer.Serialize(body);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using (HttpResponseMessage resp = await _Http.SendAsync(req, token).ConfigureAwait(false))
                {
                    return new TestResponse { StatusCode = (int)resp.StatusCode, Body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false) };
                }
            }
        }

        #endregion
    }
}
