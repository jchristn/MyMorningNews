namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
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
            bool mobileClient = String.Equals(Clean(request.GetHeader("x-client")), "mobile", StringComparison.OrdinalIgnoreCase);

            bool userLogin = email != null && password != null;
            bool credentialLogin = accessKey != null && secretKey != null;
            if (userLogin == credentialLogin) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "Supply either x-email and x-password, or x-access-key and x-secret-key.");
            if (String.IsNullOrEmpty(tenantId))
            {
                if (credentialLogin) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "x-tenant-guid is required with an access key.");
                LoginResult resolved = await ResolveTenantAsync(email!, password!, request.SourceIp, token).ConfigureAwait(false);
                if (!resolved.Success || resolved.Tenant == null) return resolved;
                tenantId = resolved.Tenant.Id;
            }

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
                if (mobileClient) await IssueRefreshTokenAsync(result, IdGenerator.GenerateRefreshTokenFamilyId(), request.SourceIp, request.UserAgent, token).ConfigureAwait(false);
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
        /// Exchange a mobile refresh token for a new session and a rotated refresh token. Reusing a token that was already
        /// rotated revokes its whole family and the sessions issued from it.
        /// </summary>
        /// <param name="refreshToken">Refresh token secret.</param>
        /// <param name="sourceIp">Source IP.</param>
        /// <param name="userAgent">User agent.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Login result with a new token and refresh token, or a 401 failure.</returns>
        public async Task<LoginResult> RefreshWithTokenAsync(string? refreshToken, string? sourceIp, string? userAgent, CancellationToken token = default)
        {
            if (!_Settings.AllowRefresh) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "Session refresh is disabled.");
            string? secret = Clean(refreshToken);
            if (secret == null) return LoginResult.Fail(400, AuthenticationOutcomeEnum.Invalid, "refreshToken is required.");
            RefreshToken? stored = await _Database.RefreshTokens.ReadByHashAsync(SecretHasher.Sha256Hex(secret), token).ConfigureAwait(false);
            if (stored == null) return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid refresh token.");

            if (stored.UsedUtc.HasValue || stored.RevokedUtc.HasValue)
            {
                if (stored.UsedUtc.HasValue) await RevokeFamilyAsync(stored, "Refresh token reuse detected.", token).ConfigureAwait(false);
                await AuditFailureAsync(stored.TenantId, stored.UserId, sourceIp, stored.UsedUtc.HasValue ? "Refresh token reuse detected; family revoked." : "Revoked refresh token.", token).ConfigureAwait(false);
                return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid refresh token.");
            }

            if (stored.ExpiresUtc < DateTime.UtcNow) return LoginResult.Fail(401, AuthenticationOutcomeEnum.Inactive, "The refresh token has expired. Sign in again.");
            Tenant? tenant = await _Database.Tenants.ReadAsync(stored.TenantId, token).ConfigureAwait(false);
            User? user = await _Database.Users.ReadAsync(stored.TenantId, stored.UserId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active || user == null || !user.Active)
            {
                await _Database.RefreshTokens.RevokeFamilyAsync(stored.FamilyId, "Account inactive.", token).ConfigureAwait(false);
                return LoginResult.Fail(401, AuthenticationOutcomeEnum.Inactive, "The account is inactive.");
            }

            stored.UsedUtc = DateTime.UtcNow;
            await _Database.RefreshTokens.UpdateAsync(stored, token).ConfigureAwait(false);
            LoginResult result = await IssueSessionAsync(tenant, user, null, AuthSchemeEnum.BearerToken, sourceIp, userAgent, token).ConfigureAwait(false);
            await IssueRefreshTokenAsync(result, stored.FamilyId, sourceIp, userAgent, token).ConfigureAwait(false);
            if (!String.IsNullOrEmpty(stored.SessionId))
            {
                AuthSession? old = await _Database.AuthSessions.ReadAsync(stored.TenantId, stored.SessionId, token).ConfigureAwait(false);
                if (old != null && !old.RevokedUtc.HasValue)
                {
                    old.RevokedUtc = DateTime.UtcNow;
                    old.RevocationReason = "Refreshed.";
                    old.Active = false;
                    await _Database.AuthSessions.UpdateAsync(old, token).ConfigureAwait(false);
                }
            }

            await _Audit.RecordAsync(new AuditRecord
            {
                TenantId = tenant.Id,
                EventType = AuditEventTypeEnum.SessionRefreshed,
                PrincipalType = PrincipalTypeEnum.User.ToString(),
                PrincipalId = user.Id,
                UserId = user.Id,
                SessionId = result.Session?.Id,
                SourceIp = sourceIp,
                AuthScheme = "RefreshToken",
                AuthenticationResult = AuthenticationOutcomeEnum.Success.ToString(),
                StatusCode = 200,
                Description = "Session refreshed with a mobile refresh token."
            }, token).ConfigureAwait(false);
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
            await _Database.RefreshTokens.RevokeBySessionAsync(sessionId, reason, token).ConfigureAwait(false);
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

        private async Task<LoginResult> ResolveTenantAsync(string email, string password, string? sourceIp, CancellationToken token)
        {
            string limiterKey = (sourceIp ?? "unknown") + "|*|" + email;
            _RateLimiter.LimitPerMinute = _Settings.LoginRateLimitPerMinute;
            if (!_RateLimiter.TryAcquire(limiterKey)) return LoginResult.Fail(429, AuthenticationOutcomeEnum.Invalid, "Too many login attempts. Try again in a minute.");
            List<User> candidates = await _Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false);
            List<string> tenantIds = candidates.Where(u => SecretHasher.VerifyPassword(password, u.PasswordSha256)).Select(u => u.TenantId).Distinct(StringComparer.Ordinal).ToList();
            if (tenantIds.Count == 0)
            {
                await AuditFailureAsync(null, email, sourceIp, "Invalid email or password (no tenant given).", token).ConfigureAwait(false);
                return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid credentials.");
            }

            if (tenantIds.Count > 1)
            {
                List<Tenant> tenants = new List<Tenant>();
                foreach (string id in tenantIds)
                {
                    Tenant? t = await _Database.Tenants.ReadAsync(id, token).ConfigureAwait(false);
                    if (t != null && t.Active) tenants.Add(t);
                }

                LoginResult choose = LoginResult.Fail(409, AuthenticationOutcomeEnum.Invalid, "Choose a tenant and sign in again with x-tenant-guid.");
                choose.TenantChoices = tenants;
                return choose;
            }

            _RateLimiter.Reset(limiterKey);
            Tenant? only = await _Database.Tenants.ReadAsync(tenantIds[0], token).ConfigureAwait(false);
            if (only == null || !only.Active) return LoginResult.Fail(401, AuthenticationOutcomeEnum.Invalid, "Invalid credentials.");
            return new LoginResult { Success = true, StatusCode = 200, Outcome = AuthenticationOutcomeEnum.Success, Tenant = only };
        }

        private async Task IssueRefreshTokenAsync(LoginResult result, string familyId, string? sourceIp, string? userAgent, CancellationToken token)
        {
            if (result.User == null || result.Tenant == null) return;
            string secret = SecretGenerator.Token(32);
            DateTime expires = DateTime.UtcNow.AddDays(_Settings.RefreshTokenLifetimeDays);
            await _Database.RefreshTokens.CreateAsync(new RefreshToken
            {
                TenantId = result.Tenant.Id,
                UserId = result.User.Id,
                SessionId = result.Session?.Id,
                FamilyId = familyId,
                TokenHash = SecretHasher.Sha256Hex(secret),
                Client = "mobile",
                SourceIp = sourceIp,
                UserAgent = userAgent,
                ExpiresUtc = expires
            }, token).ConfigureAwait(false);
            result.RefreshToken = secret;
            result.RefreshExpiresUtc = expires;
        }

        private async Task RevokeFamilyAsync(RefreshToken stored, string reason, CancellationToken token)
        {
            await _Database.RefreshTokens.RevokeFamilyAsync(stored.FamilyId, reason, token).ConfigureAwait(false);
            foreach (string sessionId in await _Database.RefreshTokens.ReadFamilySessionsAsync(stored.FamilyId, token).ConfigureAwait(false))
            {
                AuthSession? session = await _Database.AuthSessions.ReadAsync(stored.TenantId, sessionId, token).ConfigureAwait(false);
                if (session == null || session.RevokedUtc.HasValue) continue;
                session.RevokedUtc = DateTime.UtcNow;
                session.RevocationReason = reason;
                session.Active = false;
                await _Database.AuthSessions.UpdateAsync(session, token).ConfigureAwait(false);
            }
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
