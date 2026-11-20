namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Models;
    using Voltaic.Core;

    /// <summary>
    /// Authenticates Streamable HTTP requests with the same material as the REST API: <c>Authorization: Bearer</c> (or
    /// <c>x-token</c>) with a session token, or <c>x-access-key</c>, <c>x-secret-key</c>, and <c>x-tenant-guid</c>. Each
    /// credential is checked against <c>GET /v1.0/token/details</c>; a successful check is remembered for a short time
    /// under a SHA-256 fingerprint (never the secret). The MCP server holds no privileges of its own. Thread safe.
    /// </summary>
    public class CallerAuthenticator
    {
        #region Private-Members

        private readonly ApiClientFactory _Clients;
        private readonly TimeSpan _CacheFor;
        private readonly ConcurrentDictionary<string, CachedCaller> _Cache = new ConcurrentDictionary<string, CachedCaller>(StringComparer.Ordinal);
        private readonly Action<string> _Log;
        private int _Checks = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the authenticator.
        /// </summary>
        /// <param name="clients">Client factory.</param>
        /// <param name="cacheFor">How long a validated credential is trusted before checking again.</param>
        /// <param name="log">Diagnostic writer.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CallerAuthenticator(ApiClientFactory clients, TimeSpan cacheFor, Action<string> log)
        {
            _Clients = clients ?? throw new ArgumentNullException(nameof(clients));
            _Log = log ?? throw new ArgumentNullException(nameof(log));
            _CacheFor = cacheFor;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read the credentials on an HTTP request.
        /// </summary>
        /// <param name="header">Header lookup (case-insensitive), returning null when absent.</param>
        /// <returns>Credentials, possibly incomplete.</returns>
        /// <exception cref="ArgumentNullException">Lookup is null.</exception>
        public static CallerCredentials Read(Func<string, string?> header)
        {
            ArgumentNullException.ThrowIfNull(header);
            CallerCredentials c = new CallerCredentials();
            string? auth = header("Authorization");
            if (!String.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) c.Token = Clean(auth.Substring(7));
            c.Token ??= Clean(header("x-token"));
            if (c.Token == null)
            {
                c.AccessKey = Clean(header("x-access-key"));
                c.SecretKey = Clean(header("x-secret-key"));
            }

            c.TenantId = Clean(header("x-tenant-guid"));
            return c;
        }

        /// <summary>
        /// Voltaic authentication handler for <c>McpHttpServer.AuthenticationHandler</c>.
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <returns>Result; on success the claims carry the credentials for this request only.</returns>
        /// <exception cref="ArgumentNullException">Request is null.</exception>
        public async Task<AuthenticationResult> AuthenticateAsync(HttpListenerRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            CallerCredentials credentials = Read(name => request.Headers[name]);
            return await AuthenticateAsync(credentials, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate credentials against the REST API, using the short-lived cache.
        /// </summary>
        /// <param name="credentials">Credentials.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ArgumentNullException">Credentials are null.</exception>
        public async Task<AuthenticationResult> AuthenticateAsync(CallerCredentials credentials, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            if (!credentials.IsComplete)
                return AuthenticationResult.BearerChallenge(null, "invalid_request", "Send Authorization: Bearer <token>, or x-access-key, x-secret-key, and x-tenant-guid.",
                    "No My Morning News credentials were supplied.");

            string key = credentials.Fingerprint();
            DateTime now = DateTime.UtcNow;
            if (_Cache.TryGetValue(key, out CachedCaller? cached) && cached.ExpiresUtc > now) return Success(cached.Principal, credentials);

            try
            {
                TokenDetailsResponse details;
                using (MyMorningNewsClient client = _Clients.Create(credentials))
                {
                    details = await client.GetTokenDetailsAsync(token).ConfigureAwait(false);
                }

                if (!details.Valid) return Rejected("The credentials are not valid.");
                string principal = details.UserId ?? details.CredentialId ?? "unknown";
                if (_CacheFor > TimeSpan.Zero) _Cache[key] = new CachedCaller(principal, now.Add(_CacheFor));
                if (Interlocked.Increment(ref _Checks) % 200 == 0) Sweep(now);
                return Success(principal, credentials);
            }
            catch (MyMorningNewsException ex) when (ex.StatusCode == 401 || ex.StatusCode == 403)
            {
                _Cache.TryRemove(key, out CachedCaller? _);
                return Rejected("The REST API rejected the credentials (" + ex.StatusCode + ").");
            }
            catch (MyMorningNewsException ex)
            {
                _Log("[auth] credential check failed: " + ex.StatusCode + " " + ex.ErrorCode);
                return new AuthenticationResult { IsAuthenticated = false, StatusCode = 503, ErrorMessage = "The My Morning News API could not check the credentials. Try again shortly." };
            }
        }

        /// <summary>
        /// Forget every cached credential.
        /// </summary>
        public void Clear()
        {
            _Cache.Clear();
        }

        #endregion

        #region Private-Methods

        private static AuthenticationResult Success(string principal, CallerCredentials credentials)
        {
            return new AuthenticationResult { IsAuthenticated = true, Principal = principal, Claims = credentials.ToClaims(), StatusCode = 200 };
        }

        private static AuthenticationResult Rejected(string message)
        {
            return AuthenticationResult.BearerChallenge(null, "invalid_token", message, message);
        }

        private void Sweep(DateTime now)
        {
            List<string> expired = _Cache.Where(e => e.Value.ExpiresUtc <= now).Select(e => e.Key).ToList();
            foreach (string k in expired) _Cache.TryRemove(k, out CachedCaller? _);
        }

        private static string? Clean(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        #endregion
    }
}
