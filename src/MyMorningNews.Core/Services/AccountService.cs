namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Hosted accounts (plan section 33.1): public sign-up with a personal tenant, email verification, password reset,
    /// account deletion with a grace period, and the purge that follows. Every public entry point answers the same way
    /// whether or not an account exists, so it never reveals who has one. Thread safe.
    /// </summary>
    public class AccountService
    {
        #region Public-Members

        /// <summary>
        /// Shortest password accepted. Default 8.
        /// </summary>
        public const int MinimumPasswordLength = 8;

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private readonly EmailService _Email;
        private readonly TenantDefaultsService _TenantDefaults;
        private readonly PromoCodeService _Promo;
        private readonly AccountDataService _Data;
        private readonly LoginRateLimiter _Signups = new LoginRateLimiter(5) { WindowSeconds = 3600 };
        private readonly LoginRateLimiter _Mail = new LoginRateLimiter(3) { WindowSeconds = 600 };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the service.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Settings.</param>
        /// <param name="email">Email service.</param>
        /// <param name="tenantDefaults">Seeds a new tenant's prompts, models, and templates.</param>
        /// <param name="promo">Promotional codes.</param>
        /// <param name="data">Account data removal.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AccountService(DatabaseDriverBase database, ServerSettings settings, EmailService email, TenantDefaultsService tenantDefaults, PromoCodeService promo, AccountDataService data)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Email = email ?? throw new ArgumentNullException(nameof(email));
            _TenantDefaults = tenantDefaults ?? throw new ArgumentNullException(nameof(tenantDefaults));
            _Promo = promo ?? throw new ArgumentNullException(nameof(promo));
            _Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Sign up: validate, create a personal tenant and an unverified user, redeem an optional promotional code, and
        /// send the verification email. An email that already has an account gets the same answer (and, when it is an
        /// unverified sign-up, a fresh verification link).
        /// </summary>
        /// <param name="request">Sign-up details.</param>
        /// <param name="sourceIp">Caller IP for rate limiting.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Request is null.</exception>
        /// <exception cref="InvalidOperationException">Sign-up is closed.</exception>
        /// <exception cref="ArgumentException">A field is invalid.</exception>
        /// <exception cref="QuotaExceededException">Too many sign-ups from this address.</exception>
        public async Task SignUpAsync(SignupRequest request, string? sourceIp, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            HostingSettings h = _Settings.Hosting;
            if (!h.PublicSignupEnabled) throw new InvalidOperationException("Sign-up is not open on this server.");
            string email = (request.Email ?? String.Empty).Trim().ToLowerInvariant();
            if (email.Length < 3 || email.Length > 256 || !email.Contains('@') || email.StartsWith("@", StringComparison.Ordinal) || email.EndsWith("@", StringComparison.Ordinal))
                throw new ArgumentException("Enter a valid email address.");
            string domain = email.Substring(email.LastIndexOf('@') + 1);
            if (h.BlockedEmailDomains.Any(d => String.Equals(d.Trim(), domain, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Use a permanent email address.");
            ValidatePassword(request.Password);
            if (!request.AcceptTerms) throw new ArgumentException("Accept the terms of service to sign up.");
            if (!request.ConfirmAge) throw new ArgumentException("You must be at least " + h.MinimumAge + " years old to sign up.");
            _Signups.LimitPerMinute = Math.Max(1, h.SignupsPerIpPerHour);
            if (!_Signups.TryAcquire("signup|" + (sourceIp ?? "unknown"))) throw new QuotaExceededException("Too many sign-ups from this address. Try again later.");

            List<User> existing = await _Database.Users.ReadAllByEmailAsync(email, token).ConfigureAwait(false);
            if (existing.Any())
            {
                User? pending = existing.FirstOrDefault(u => u.SelfSignup && !u.EmailVerifiedUtc.HasValue && u.Active);
                if (pending != null) await SendVerificationAsync(pending, sourceIp, token).ConfigureAwait(false);
                return;
            }

            if (!String.IsNullOrWhiteSpace(request.PromoCode))
            {
                PromoCheck check = await _Promo.CheckAsync(request.PromoCode, null, null, true, token).ConfigureAwait(false);
                if (!check.Valid) throw new ArgumentException(check.Message);
            }

            string display = String.IsNullOrWhiteSpace(request.DisplayName) ? email.Substring(0, email.IndexOf('@')) : request.DisplayName.Trim();
            EntitlementService entitlements = new EntitlementService(_Database, _Settings);
            await entitlements.SignupPlanAsync(token).ConfigureAwait(false);
            if (display.Length > 128) display = display.Substring(0, 128);
            Tenant tenant = await _Database.Tenants.CreateAsync(new Tenant
            {
                Name = (display.Length > 117 ? display.Substring(0, 117).TrimEnd() : display) + " (personal)", Description = "Personal tenant created by sign-up.", TimeZone = String.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone.Trim(), IsPersonal = true
            }, token).ConfigureAwait(false);
            User user;
            try
            {
                DateTime now = DateTime.UtcNow;
                user = await _Database.Users.CreateAsync(new User
                {
                    TenantId = tenant.Id, Email = email, FirstName = display, PasswordSha256 = SecretHasher.Sha256Hex(request.Password!), IsTenantAdmin = true,
                    TimeZone = tenant.TimeZone, Locale = String.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale.Trim(), SelfSignup = true,
                    EmailVerifiedUtc = h.RequireEmailVerification ? null : now, TermsVersion = h.TermsVersion, TermsAcceptedUtc = now
                }, token).ConfigureAwait(false);
                await _TenantDefaults.EnsureAsync(tenant.Id, token).ConfigureAwait(false);
                await entitlements.CaptureSignupPolicyAsync(tenant.Id, user.Id, token).ConfigureAwait(false);
                if (!String.IsNullOrWhiteSpace(request.PromoCode))
                    await _Promo.RedeemAsync(tenant.Id, user.Id, request.PromoCode, PromoChannelEnum.Signup, true, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Nothing is sent until initialization and atomic code redemption succeed.
                await _Database.Tenants.DeleteAsync(tenant.Id, CancellationToken.None).ConfigureAwait(false);
                if (ex is PromoCodeException) throw new ArgumentException(ex.Message, nameof(request.PromoCode), ex);
                throw;
            }

            if (h.RequireEmailVerification) await SendVerificationAsync(user, sourceIp, token).ConfigureAwait(false);
            else await SendWelcomeAsync(user, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a new verification link to an unverified sign-up account. Silent for unknown emails.
        /// </summary>
        /// <param name="email">Email.</param>
        /// <param name="sourceIp">Caller IP for rate limiting.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ResendVerificationAsync(string? email, string? sourceIp, CancellationToken token = default)
        {
            string e = (email ?? String.Empty).Trim().ToLowerInvariant();
            if (e.Length == 0 || !_Mail.TryAcquire("verify|" + e)) return;
            foreach (User u in (await _Database.Users.ReadAllByEmailAsync(e, token).ConfigureAwait(false)).Where(u => u.SelfSignup && !u.EmailVerifiedUtc.HasValue && u.Active))
                await SendVerificationAsync(u, sourceIp, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify an email from the link token. Sends the welcome email the first time.
        /// </summary>
        /// <param name="rawToken">Token from the link.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The verified user.</returns>
        /// <exception cref="AccountLinkException">The link is invalid (LinkInvalid), already used (LinkUsed), or expired (LinkExpired).</exception>
        public async Task<User> VerifyEmailAsync(string? rawToken, CancellationToken token = default)
        {
            AccountToken t = await UseTokenAsync(rawToken, AccountTokenKindEnum.VerifyEmail, token).ConfigureAwait(false);
            User user = await _Database.Users.ReadAsync(t.TenantId, t.UserId, token).ConfigureAwait(false) ?? throw new AccountLinkException(AccountLinkException.Invalid, "This link is no longer valid.");
            if (!user.EmailVerifiedUtc.HasValue)
            {
                user.EmailVerifiedUtc = DateTime.UtcNow;
                await _Database.Users.UpdateAsync(user, token).ConfigureAwait(false);
                await SendWelcomeAsync(user, token).ConfigureAwait(false);
            }

            return user;
        }

        /// <summary>
        /// Send password reset links to every active account with this email. Silent for unknown emails.
        /// </summary>
        /// <param name="email">Email.</param>
        /// <param name="sourceIp">Caller IP.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ForgotPasswordAsync(string? email, string? sourceIp, CancellationToken token = default)
        {
            string e = (email ?? String.Empty).Trim().ToLowerInvariant();
            if (e.Length == 0 || !_Mail.TryAcquire("reset|" + e)) return;
            foreach (User u in (await _Database.Users.ReadAllByEmailAsync(e, token).ConfigureAwait(false)).Where(u => u.Active && !u.IsProtected))
            {
                string raw = await IssueAsync(u, AccountTokenKindEnum.ResetPassword, TimeSpan.FromMinutes(_Settings.Hosting.PasswordResetMinutes), sourceIp, token).ConfigureAwait(false);
                Tenant? tenant = await _Database.Tenants.ReadAsync(u.TenantId, token).ConfigureAwait(false);
                await _Email.SendAccountEmailAsync(u, EmailTemplateEnum.PasswordReset, "Reset your password",
                    new List<string>
                    {
                        "Someone asked to reset the password for your My Morning News account" + (tenant == null || tenant.IsPersonal ? "." : " in " + tenant.Name + "."),
                        "The link works once, for " + _Settings.Hosting.PasswordResetMinutes + " minutes. If you did not ask for it, ignore this email; your password stays the same."
                    },
                    new EmailLink("Choose a new password", _Settings.Rest.AppBaseUrl + "/reset-password?token=" + Uri.EscapeDataString(raw)), "reset:" + u.Id + ":" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Set a new password from a reset link, end every session, and send a confirmation.
        /// </summary>
        /// <param name="rawToken">Token from the link.</param>
        /// <param name="newPassword">New password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user.</returns>
        /// <exception cref="ArgumentException">The password is too short.</exception>
        /// <exception cref="AccountLinkException">The link is invalid (LinkInvalid), already used (LinkUsed), or expired (LinkExpired).</exception>
        public async Task<User> ResetPasswordAsync(string? rawToken, string? newPassword, CancellationToken token = default)
        {
            ValidatePassword(newPassword);
            AccountToken t = await UseTokenAsync(rawToken, AccountTokenKindEnum.ResetPassword, token).ConfigureAwait(false);
            User user = await _Database.Users.ReadAsync(t.TenantId, t.UserId, token).ConfigureAwait(false) ?? throw new AccountLinkException(AccountLinkException.Invalid, "This link is no longer valid.");
            user.PasswordSha256 = SecretHasher.Sha256Hex(newPassword!);
            if (user.SelfSignup && !user.EmailVerifiedUtc.HasValue) user.EmailVerifiedUtc = DateTime.UtcNow;
            await _Database.Users.UpdateAsync(user, token).ConfigureAwait(false);
            await RevokeSessionsAsync(user, "Password reset.", token).ConfigureAwait(false);
            await _Email.SendAccountEmailAsync(user, EmailTemplateEnum.PasswordChanged, "Your password was changed",
                new List<string> { "The password for your My Morning News account was just changed and every device was signed out.", "If this was not you, reset your password now and write to hello@latticetech.ai." },
                new EmailLink("Sign in", _Settings.Rest.AppBaseUrl + "/login"), "changed:" + user.Id + ":" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(false);
            return user;
        }

        /// <summary>
        /// Request account deletion: sign out everywhere, schedule the purge after the grace period, and email a
        /// confirmation. Signing in again during the grace period cancels it.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>When the data will be purged (UTC).</returns>
        /// <exception cref="ArgumentNullException">User is null.</exception>
        /// <exception cref="ConflictException">The account is protected.</exception>
        public async Task<DateTime> RequestDeletionAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            if (user.IsProtected || user.IsAdmin) throw new ConflictException("This account is protected and cannot be deleted.");
            await _Data.StopBillingAsync(user.TenantId, user.Id, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            DateTime purge = now.AddDays(_Settings.Hosting.DeletionGraceDays);
            user.DeletionRequestedUtc = now;
            await _Database.Users.UpdateAsync(user, token).ConfigureAwait(false);
            Tenant? tenant = await _Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false);
            if (tenant != null && tenant.IsPersonal)
            {
                tenant.PurgeAfterUtc = purge;
                await _Database.Tenants.UpdateAsync(tenant, token).ConfigureAwait(false);
            }

            await RevokeSessionsAsync(user, "Account deletion requested.", token).ConfigureAwait(false);
            await _Email.SendAccountEmailAsync(user, EmailTemplateEnum.AccountDeletion, "Your account will be deleted",
                new List<string>
                {
                    "We received your request to delete your My Morning News account. Your topics, briefings, findings, feedback, and audio will be permanently deleted on " + purge.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture) + ".",
                    "Changed your mind? Sign in before then and the deletion is cancelled. This does not restart canceled subscription renewals.",
                    "Web and Google Play renewals have been stopped. If you subscribed through Apple, cancel in Settings > Apple Account > Subscriptions; deleting this account does not cancel an Apple subscription."
                },
                new EmailLink("Sign in to keep my account", _Settings.Rest.AppBaseUrl + "/login"), "deletion:" + user.Id + ":" + now.Ticks, token).ConfigureAwait(false);
            return purge;
        }

        /// <summary>
        /// Cancel a pending deletion (called on a successful sign-in).
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a pending deletion was cancelled.</returns>
        /// <exception cref="ArgumentNullException">User is null.</exception>
        public async Task<bool> CancelDeletionAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            if (!user.DeletionRequestedUtc.HasValue) return false;
            user.DeletionRequestedUtc = null;
            await _Database.Users.UpdateAsync(user, token).ConfigureAwait(false);
            Tenant? tenant = await _Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false);
            if (tenant != null && tenant.PurgeAfterUtc.HasValue)
            {
                tenant.PurgeAfterUtc = null;
                await _Database.Tenants.UpdateAsync(tenant, token).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// Purge accounts whose grace period has ended: personal tenants are deleted whole; users in shared tenants are
        /// removed with their data.
        /// </summary>
        /// <param name="nowUtc">Current time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Accounts purged.</returns>
        public async Task<int> PurgeDueAsync(DateTime nowUtc, CancellationToken token = default)
        {
            int purged = 0;
            DateTime cutoff = nowUtc.AddDays(-_Settings.Hosting.DeletionGraceDays);
            foreach (User u in await _Database.Users.ListDeletionDueAsync(cutoff, token).ConfigureAwait(false))
            {
                Tenant? tenant = await _Database.Tenants.ReadAsync(u.TenantId, token).ConfigureAwait(false);
                await _Data.DeleteUserAsync(u.TenantId, u.Id, token).ConfigureAwait(false);
                if (tenant != null && tenant.IsPersonal && await _Database.Users.CountAsync(new UserFilter { TenantId = tenant.Id }, token).ConfigureAwait(false) == 0)
                    await _Database.Tenants.DeleteAsync(tenant.Id, token).ConfigureAwait(false);
                purged++;
            }

            return purged;
        }

        /// <summary>
        /// Whether a sign-in must be refused because the email is not verified yet.
        /// </summary>
        /// <param name="user">User.</param>
        /// <returns>True when the user must verify first.</returns>
        /// <exception cref="ArgumentNullException">User is null.</exception>
        public bool NeedsVerification(User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            return user.SelfSignup && !user.EmailVerifiedUtc.HasValue && _Settings.Hosting.RequireEmailVerification;
        }

        #endregion

        #region Private-Methods

        private static void ValidatePassword(string? password)
        {
            if (String.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength) throw new ArgumentException("Use a password of at least " + MinimumPasswordLength + " characters.");
            if (password.Length > 256) throw new ArgumentException("The password is too long.");
        }

        private async Task SendVerificationAsync(User user, string? sourceIp, CancellationToken token)
        {
            string raw = await IssueAsync(user, AccountTokenKindEnum.VerifyEmail, TimeSpan.FromHours(_Settings.Hosting.VerificationHours), sourceIp, token).ConfigureAwait(false);
            await _Email.SendAccountEmailAsync(user, EmailTemplateEnum.VerifyEmail, "Confirm your email",
                new List<string> { "Welcome to My Morning News. Confirm this address to finish creating your account.", "The link works for " + _Settings.Hosting.VerificationHours + " hours. If you did not sign up, ignore this email." },
                new EmailLink("Confirm my email", _Settings.Rest.AppBaseUrl + "/verify?token=" + Uri.EscapeDataString(raw)), "verify:" + user.Id + ":" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(false);
        }

        private async Task SendWelcomeAsync(User user, CancellationToken token)
        {
            await _Email.SendAccountEmailAsync(user, EmailTemplateEnum.Welcome, "Welcome to My Morning News",
                new List<string>
                {
                    "Your account is ready. Start by describing a topic you want to stay on top of; the wizard turns it into research that runs on your schedule.",
                    "Each run brings ranked findings with their sources, predictions, and suggestions, by email, on the web, as audio, and in the iOS and Android apps."
                },
                new EmailLink("Create my first topic", _Settings.Rest.AppBaseUrl + "/dashboard/topics/new"), "welcome:" + user.Id, token).ConfigureAwait(false);
        }

        private async Task<string> IssueAsync(User user, AccountTokenKindEnum kind, TimeSpan lifetime, string? sourceIp, CancellationToken token)
        {
            string raw = SecretGenerator.Token(32);
            await _Database.AccountTokens.CreateAsync(new AccountToken
            {
                TenantId = user.TenantId, UserId = user.Id, Kind = kind, TokenHash = SecretHasher.Sha256Hex(raw), ExpiresUtc = DateTime.UtcNow.Add(lifetime), SourceIp = sourceIp
            }, token).ConfigureAwait(false);
            return raw;
        }

        private async Task<AccountToken> UseTokenAsync(string? rawToken, AccountTokenKindEnum kind, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200) throw new AccountLinkException(AccountLinkException.Invalid, "This link is not valid.");
            AccountToken? t = await _Database.AccountTokens.ReadByHashAsync(SecretHasher.Sha256Hex(rawToken.Trim()), token).ConfigureAwait(false);
            if (t == null || t.Kind != kind || !t.Active) throw new AccountLinkException(AccountLinkException.Invalid, "This link is not valid.");
            // Holding the token proves the reader received the email, so saying it was used reveals nothing new.
            if (t.UsedUtc.HasValue)
                throw new AccountLinkException(AccountLinkException.Used, kind == AccountTokenKindEnum.VerifyEmail
                    ? "This link was already used, so your email address is confirmed. Sign in to continue."
                    : "This link was already used. Ask for a new one if you still need to reset your password.");
            if (t.ExpiresUtc < DateTime.UtcNow) throw new AccountLinkException(AccountLinkException.Expired, "This link has expired. Ask for a new one.");
            t.UsedUtc = DateTime.UtcNow;
            await _Database.AccountTokens.UpdateAsync(t, token).ConfigureAwait(false);
            return t;
        }

        private async Task RevokeSessionsAsync(User user, string reason, CancellationToken token)
        {
            DateTime now = DateTime.UtcNow;
            foreach (AuthSession s in (await _Database.AuthSessions.EnumerateAsync(new AuthSessionFilter { TenantId = user.TenantId, UserId = user.Id, Active = true, PageSize = 1000 }, token).ConfigureAwait(false)).Items)
            {
                s.RevokedUtc = now;
                s.RevocationReason = reason;
                s.Active = false;
                await _Database.AuthSessions.UpdateAsync(s, token).ConfigureAwait(false);
                await _Database.RefreshTokens.RevokeBySessionAsync(s.Id, reason, token).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
