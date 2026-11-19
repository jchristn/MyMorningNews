namespace MyMorningNews.Core.Services
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Authenticates requests and issues, refreshes, and revokes session tokens. Exactly one scheme may drive a request;
    /// conflicting material is rejected. Tokens are AES-256-GCM encrypted payloads bound to a revocable session row.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class AuthenticationService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly AuthSettings _Settings;
        private readonly AuditService _Audit;
        private readonly LoginRateLimiter _RateLimiter;
        private readonly CryptoProtector _TokenProtector;
        private readonly CryptoProtector _SecretProtector;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Authentication settings (read live).</param>
        /// <param name="audit">Audit service.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AuthenticationService(DatabaseDriverBase database, AuthSettings settings, AuditService audit)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(audit);
            _Database = database;
            _Settings = settings;
            _Audit = audit;
            _RateLimiter = new LoginRateLimiter(settings.LoginRateLimitPerMinute);
            _TokenProtector = new CryptoProtector(settings.SigningKey, "session-token");
            _SecretProtector = new CryptoProtector(settings.SigningKey, "credential-secret");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Encrypt a raw credential secret for storage.
        /// </summary>
        /// <param name="secret">Raw secret.</param>
        /// <returns>Encrypted value.</returns>
        public string ProtectSecret(string secret)
        {
            return _SecretProtector.Protect(secret);
        }

        /// <summary>
        /// Authenticate a request from its headers.
        /// </summary>
        /// <param name="request">Authentication material.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authentication result.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        public async Task<AuthenticationContext> AuthenticateAsync(AuthenticationRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            string? bearer = ReadBearer(request.GetHeader("authorization"));
            string? xToken = Clean(request.GetHeader("x-token"));
            string? apiKey = Clean(request.GetHeader("x-api-key"));
            string? accessKey = Clean(request.GetHeader("x-access-key"));
            string? secretKey = Clean(request.GetHeader("x-secret-key"));
            string? tenantHint = Clean(request.GetHeader("x-tenant-guid"));

            if (Clean(request.GetHeader("x-password")) != null)
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "Login headers are accepted only by POST /v1.0/token.", AuthSchemeEnum.PasswordHeaders);

            if (bearer != null && xToken != null && !SecretHasher.FixedTimeEquals(bearer, xToken))
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "Authorization and x-token carry different tokens.", AuthSchemeEnum.BearerToken);

            string? sessionToken = bearer ?? xToken;
            int schemes = (sessionToken != null ? 1 : 0) + (apiKey != null ? 1 : 0) + (accessKey != null ? 1 : 0);
            if (schemes == 0) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.NotAttempted, "No credentials were supplied.");
            if (schemes > 1) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "Only one authentication scheme may be used per request.");

            if (sessionToken != null) return await AuthenticateSessionAsync(sessionToken, bearer != null ? AuthSchemeEnum.BearerToken : AuthSchemeEnum.XToken, tenantHint, token).ConfigureAwait(false);
            if (apiKey != null) return await AuthenticateAdminKeyAsync(apiKey, tenantHint, token).ConfigureAwait(false);
            return await AuthenticateCredentialAsync(accessKey!, secretKey, tenantHint, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Issue a session token from login headers: either email, password, and tenant, or access key, secret key, and tenant.
        /// </summary>
        /// <param name="request">Authentication material.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Login result.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        public async Task<LoginResult> LoginAsync(AuthenticationRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            string? email = Clean(request.GetHeader("x-email"));
            string? password = Clean(request.GetHeader("x-password"));
            string? accessKey = Clean(request.GetHeader("x-access-key"));
            string? secretKey = Clean(request.GetHeader("x-secret-key"));
            string? tenantId = Clean(request.GetHeader("x-tenant-guid"));

            if (String.IsNullOrEmpty(tenantId)) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "x-tenant-guid is required.");
            bool userLogin = email != null && password != null;
            bool credentialLogin = accessKey != null && secretKey != null;
            if (userLogin == credentialLogin) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "Supply either x-email and x-password, or x-access-key and x-secret-key.");

            string limiterKey = (request.SourceIp ?? "unknown") + "|" + tenantId + "|" + (email ?? accessKey);
            _RateLimiter.LimitPerMinute = _Settings.LoginRateLimitPerMinute;
            if (!_RateLimiter.TryAcquire(limiterKey)) return LoginResult.Fail(429, AuthenticationOutcomeEnum.Invalid, "Too many login attempts. Try again in a minute.");

            Tenant? tenant = await _Database.Tenants.ReadAsync(tenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active)
            {
                await AuditFailureAsync(null, email ?? accessKey, request.SourceIp, "Unknown or inactive tenant.", token).ConfigureAwait(false);
                return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid credentials.");
            }

            User? user;
            Credential? credential = null;
            if (userLogin)
            {
                user = await FindUserByEmailAsync(tenant.Id, email!, token).ConfigureAwait(false);
                if (user == null || !SecretHasher.VerifyPassword(password, user.PasswordSha256))
                {
                    await AuditFailureAsync(tenant.Id, email, request.SourceIp, "Invalid email or password.", token).ConfigureAwait(false);
                    return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid credentials.");
                }
            }
            else
            {
                credential = await FindCredentialAsync(tenant.Id, accessKey!, token).ConfigureAwait(false);
                if (credential == null || !SecretHasher.FixedTimeEquals(_SecretProtector.Unprotect(credential.SecretKeyEncrypted), secretKey))
                {
                    await AuditFailureAsync(tenant.Id, accessKey, request.SourceIp, "Invalid access key or secret key.", token).ConfigureAwait(false);
                    return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid credentials.");
                }

                user = await _Database.Users.ReadAsync(tenant.Id, credential.UserId, token).ConfigureAwait(false);
                if (!credential.Active || (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value < DateTime.UtcNow))
                    return LoginResult.Fail(401, AuthenticationOutcomeEnum.Inactive, "The credential is inactive or expired.");
            }

            if (user == null || !user.Active) return LoginResult.Fail(401, AuthenticationOutcomeEnum.Inactive, "The account is inactive.");

            _RateLimiter.Reset(limiterKey);
            LoginResult result = await IssueSessionAsync(tenant, user, credential, userLogin ? AuthSchemeEnum.PasswordHeaders : AuthSchemeEnum.AccessKeySecret, request.SourceIp, request.UserAgent, token).ConfigureAwait(false);

            if (userLogin)
            {
                user.LastLoginUtc = DateTime.UtcNow;
                await _Database.Users.UpdateAsync(user, token).ConfigureAwait(false);
            }

            await _Audit.RecordAsync(new AuditRecord
            {
                TenantId = tenant.Id,
                EventType = AuditEventTypeEnum.SessionIssued,
                PrincipalType = (credential != null ? PrincipalTypeEnum.Credential : PrincipalTypeEnum.User).ToString(),
                PrincipalId = credential?.Id ?? user.Id,
                UserId = user.Id,
                CredentialId = credential?.Id,
                SessionId = result.Session?.Id,
                SourceIp = request.SourceIp,
                AuthScheme = (userLogin ? AuthSchemeEnum.PasswordHeaders : AuthSchemeEnum.AccessKeySecret).ToString(),
                AuthenticationResult = AuthenticationOutcomeEnum.Success.ToString(),
                StatusCode = 200,
                Description = "Session issued."
            }, token).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Issue a replacement token for the current session and revoke the old one.
        /// </summary>
        /// <param name="ctx">Authenticated request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Login result.</returns>
        /// <exception cref="ArgumentNullException">ctx is null.</exception>
        public async Task<LoginResult> RefreshAsync(RequestContext ctx, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            if (!_Settings.AllowRefresh) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "Session refresh is disabled.");
            if (ctx.SessionId == null || ctx.TenantId == null || ctx.Authentication.Tenant == null || ctx.Authentication.User == null)
                return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "Only session tokens can be refreshed.");

            LoginResult result = await IssueSessionAsync(ctx.Authentication.Tenant, ctx.Authentication.User, ctx.Authentication.Credential,
                ctx.Authentication.Scheme ?? AuthSchemeEnum.BearerToken, ctx.SourceIp, ctx.UserAgent, token).ConfigureAwait(false);
            await RevokeAsync(ctx.TenantId, ctx.SessionId, "Refreshed.", token).ConfigureAwait(false);
            await _Audit.RecordAsync(AuditEventTypeEnum.SessionRefreshed, ctx, 200, "Session refreshed.", token).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Revoke a session.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="reason">Reason.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a session was revoked.</returns>
        public async Task<bool> RevokeAsync(string tenantId, string sessionId, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            AuthSession? session = await _Database.AuthSessions.ReadAsync(tenantId, sessionId, token).ConfigureAwait(false);
            if (session == null || session.RevokedUtc.HasValue) return false;
            session.RevokedUtc = DateTime.UtcNow;
            session.RevocationReason = reason;
            session.Active = false;
            await _Database.AuthSessions.UpdateAsync(session, token).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Decode a token without validating the session (for the token details endpoint).
        /// </summary>
        /// <param name="tokenValue">Token.</param>
        /// <returns>Payload, or null when the token is malformed.</returns>
        public SessionTokenPayload? Decode(string? tokenValue)
        {
            string? json = _TokenProtector.Unprotect(tokenValue);
            if (json == null) return null;
            try
            {
                return Serializer.Deserialize<SessionTokenPayload>(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        #endregion

        #region Private-Methods

        private static string? Clean(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            return value.Trim();
        }

        private static string? ReadBearer(string? header)
        {
            if (String.IsNullOrWhiteSpace(header)) return null;
            string h = header.Trim();
            if (!h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
            return Clean(h.Substring(7));
        }

        private async Task<User?> FindUserByEmailAsync(string tenantId, string email, CancellationToken token)
        {
            EnumerationResult<User> users = await _Database.Users.EnumerateAsync(new UserFilter { TenantId = tenantId, Email = email.Trim().ToLowerInvariant(), PageSize = 2 }, token).ConfigureAwait(false);
            if (users.Items.Count == 0)
                users = await _Database.Users.EnumerateAsync(new UserFilter { TenantId = tenantId, Email = email.Trim(), PageSize = 2 }, token).ConfigureAwait(false);
            return users.Items.FirstOrDefault();
        }

        private async Task<Credential?> FindCredentialAsync(string tenantId, string accessKey, CancellationToken token)
        {
            EnumerationResult<Credential> creds = await _Database.Credentials.EnumerateAsync(new CredentialFilter { TenantId = tenantId, AccessKey = accessKey, PageSize = 2 }, token).ConfigureAwait(false);
            return creds.Items.FirstOrDefault();
        }

        private async Task<LoginResult> IssueSessionAsync(Tenant tenant, User user, Credential? credential, AuthSchemeEnum scheme, string? sourceIp, string? userAgent, CancellationToken token)
        {
            DateTime now = DateTime.UtcNow;
            AuthSession session = new AuthSession
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                CredentialId = credential?.Id,
                PrincipalType = credential != null ? PrincipalTypeEnum.Credential : PrincipalTypeEnum.User,
                AuthScheme = scheme,
                TokenId = SecretGenerator.Token(24),
                SourceIp = sourceIp,
                UserAgent = userAgent,
                ExpiresUtc = now.AddMinutes(_Settings.SessionLifetimeMinutes)
            };
            session = await _Database.AuthSessions.CreateAsync(session, token).ConfigureAwait(false);

            SessionTokenPayload payload = new SessionTokenPayload
            {
                SessionId = session.Id,
                TokenId = session.TokenId,
                PrincipalType = session.PrincipalType,
                TenantId = tenant.Id,
                UserId = user.Id,
                CredentialId = credential?.Id,
                Scheme = scheme,
                IssuedUtc = now,
                ExpiresUtc = session.ExpiresUtc
            };

            return new LoginResult
            {
                Success = true,
                StatusCode = 200,
                Outcome = AuthenticationOutcomeEnum.Success,
                Token = _TokenProtector.Protect(Serializer.Serialize(payload)),
                Session = session,
                User = user,
                Credential = credential,
                Tenant = tenant
            };
        }

        private async Task<AuthenticationContext> AuthenticateSessionAsync(string tokenValue, AuthSchemeEnum scheme, string? tenantHint, CancellationToken token)
        {
            SessionTokenPayload? payload = Decode(tokenValue);
            if (payload == null) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "The token is malformed.", scheme);
            if (tenantHint != null && !String.Equals(tenantHint, payload.TenantId, StringComparison.Ordinal))
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "The token belongs to a different tenant.", scheme);

            AuthSession? session = await _Database.AuthSessions.ReadAsync(payload.TenantId, payload.SessionId, token).ConfigureAwait(false);
            if (session == null) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.NotFound, "The session was not found.", scheme);
            if (!SecretHasher.FixedTimeEquals(session.TokenId, payload.TokenId)) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "The token does not match the session.", scheme);
            DateTime now = DateTime.UtcNow;
            if (!session.Active || session.RevokedUtc.HasValue || session.ExpiresUtc < now)
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Inactive, "The session has expired or was revoked.", scheme);

            Tenant? tenant = await _Database.Tenants.ReadAsync(session.TenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Inactive, "The tenant is inactive.", scheme);

            User? user = session.UserId != null ? await _Database.Users.ReadAsync(tenant.Id, session.UserId, token).ConfigureAwait(false) : null;
            if (user == null || !user.Active) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Inactive, "The user is inactive.", scheme);

            Credential? credential = null;
            if (session.CredentialId != null)
            {
                credential = await _Database.Credentials.ReadAsync(tenant.Id, session.CredentialId, token).ConfigureAwait(false);
                if (credential == null || !credential.Active || (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value < now))
                    return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Inactive, "The credential is inactive or expired.", scheme);
            }

            if (!session.LastUsedUtc.HasValue || (now - session.LastUsedUtc.Value).TotalSeconds > 60)
            {
                session.LastUsedUtc = now;
                await _Database.AuthSessions.UpdateAsync(session, token).ConfigureAwait(false);
            }

            return new AuthenticationContext
            {
                Outcome = AuthenticationOutcomeEnum.Success,
                Scheme = scheme,
                PrincipalType = session.PrincipalType,
                PrincipalId = credential?.Id ?? user.Id,
                SessionId = session.Id,
                Tenant = tenant,
                User = user,
                Credential = credential
            };
        }

        private async Task<AuthenticationContext> AuthenticateAdminKeyAsync(string apiKey, string? tenantHint, CancellationToken token)
        {
            if (String.IsNullOrEmpty(_Settings.AdminApiKey) || !SecretHasher.FixedTimeEquals(apiKey, _Settings.AdminApiKey))
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "The administration key is invalid.", AuthSchemeEnum.AdminApiKey);

            Tenant? tenant = null;
            if (tenantHint != null)
            {
                tenant = await _Database.Tenants.ReadAsync(tenantHint, token).ConfigureAwait(false);
                if (tenant == null) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.NotFound, "The tenant was not found.", AuthSchemeEnum.AdminApiKey);
            }

            return new AuthenticationContext
            {
                Outcome = AuthenticationOutcomeEnum.Success,
                Scheme = AuthSchemeEnum.AdminApiKey,
                PrincipalType = PrincipalTypeEnum.Administrator,
                PrincipalId = "system",
                IsSystemAdministrator = true,
                Tenant = tenant
            };
        }

        private async Task<AuthenticationContext> AuthenticateCredentialAsync(string accessKey, string? secretKey, string? tenantHint, CancellationToken token)
        {
            if (secretKey == null) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "x-access-key requires x-secret-key.", AuthSchemeEnum.AccessKeySecret);
            if (tenantHint == null) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "x-tenant-guid is required with access keys.", AuthSchemeEnum.AccessKeySecret);

            Tenant? tenant = await _Database.Tenants.ReadAsync(tenantHint, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "Invalid credentials.", AuthSchemeEnum.AccessKeySecret);
            Credential? credential = await FindCredentialAsync(tenant.Id, accessKey, token).ConfigureAwait(false);
            if (credential == null || !SecretHasher.FixedTimeEquals(_SecretProtector.Unprotect(credential.SecretKeyEncrypted), secretKey))
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Invalid, "Invalid credentials.", AuthSchemeEnum.AccessKeySecret);

            DateTime now = DateTime.UtcNow;
            if (!credential.Active || (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value < now))
                return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Inactive, "The credential is inactive or expired.", AuthSchemeEnum.AccessKeySecret);

            User? user = await _Database.Users.ReadAsync(tenant.Id, credential.UserId, token).ConfigureAwait(false);
            if (user == null || !user.Active) return AuthenticationContext.Fail(AuthenticationOutcomeEnum.Inactive, "The owning user is inactive.", AuthSchemeEnum.AccessKeySecret);

            if (!credential.LastUsedUtc.HasValue || (now - credential.LastUsedUtc.Value).TotalSeconds > 60)
            {
                credential.LastUsedUtc = now;
                await _Database.Credentials.UpdateAsync(credential, token).ConfigureAwait(false);
            }

            return new AuthenticationContext
            {
                Outcome = AuthenticationOutcomeEnum.Success,
                Scheme = AuthSchemeEnum.AccessKeySecret,
                PrincipalType = PrincipalTypeEnum.Credential,
                PrincipalId = credential.Id,
                Tenant = tenant,
                User = user,
                Credential = credential
            };
        }

        private async Task AuditFailureAsync(string? tenantId, string? principal, string? sourceIp, string reason, CancellationToken token)
        {
            await _Audit.RecordAsync(new AuditRecord
            {
                TenantId = tenantId,
                EventType = AuditEventTypeEnum.AuthFailure,
                PrincipalId = principal,
                SourceIp = sourceIp,
                AuthenticationResult = AuthenticationOutcomeEnum.Invalid.ToString(),
                Reason = reason,
                StatusCode = 401
            }, token).ConfigureAwait(false);
        }

        #endregion
    }
}
