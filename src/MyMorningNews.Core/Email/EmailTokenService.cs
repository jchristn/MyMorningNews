namespace MyMorningNews.Core.Email
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Issues and validates email link tokens (plan section 23.4): 32 random bytes, URL-safe Base64, only the SHA-256 hash
    /// stored, bound to one item and its allowed actions, expiring, revocable, and invalid once the user or tenant is
    /// deactivated. Thread safety: safe for concurrent use.
    /// </summary>
    public class EmailTokenService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Server settings.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public EmailTokenService(DatabaseDriverBase database, ServerSettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Issue a token.
        /// </summary>
        /// <param name="message">Email the link is in.</param>
        /// <param name="userId">User the action is attributed to.</param>
        /// <param name="kind">Feedback or unsubscribe.</param>
        /// <param name="targetType">Item type (feedback).</param>
        /// <param name="targetId">Item ID, or the unsubscribe scope.</param>
        /// <param name="topicId">Topic, or null.</param>
        /// <param name="actions">Allowed actions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The token and its secret.</returns>
        public async Task<IssuedToken> IssueAsync(EmailMessage message, string userId, ActionTokenKindEnum kind, FeedbackTargetTypeEnum targetType, string targetId, string? topicId, IReadOnlyList<string> actions, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            string secret = NewSecret();
            EmailActionToken t = await _Database.EmailActionTokens.CreateAsync(new EmailActionToken
            {
                TenantId = message.TenantId,
                UserId = userId,
                EmailMessageId = message.Id,
                Kind = kind,
                TargetType = targetType,
                TargetId = targetId,
                TopicId = topicId,
                Recipient = message.Recipient,
                AllowedActions = new List<string>(actions),
                TokenHash = Hash(secret),
                ExpiresUtc = DateTime.UtcNow.AddDays(_Settings.Email.ActionTokenLifetimeDays)
            }, token).ConfigureAwait(false);
            return new IssuedToken { Token = t, Secret = secret };
        }

        /// <summary>
        /// Validate a secret: known, not expired or revoked, and its user and tenant still active.
        /// </summary>
        /// <param name="secret">Secret from the URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token, or null (callers answer the same way for every failure).</returns>
        public async Task<EmailActionToken?> ValidateAsync(string? secret, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(secret) || secret.Length < 20 || secret.Length > 100) return null;
            EmailActionToken? t = await _Database.EmailActionTokens.ReadByHashAsync(Hash(secret), token).ConfigureAwait(false);
            if (t == null || t.RevokedUtc.HasValue || t.ExpiresUtc < DateTime.UtcNow || !t.Active) return null;
            Tenant? tenant = await _Database.Tenants.ReadAsync(t.TenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return null;
            User? user = await _Database.Users.ReadAsync(t.TenantId, t.UserId, token).ConfigureAwait(false);
            if (user == null || !user.Active) return null;
            return t;
        }

        /// <summary>
        /// Record a landing page view or a submission.
        /// </summary>
        /// <param name="t">Token.</param>
        /// <param name="submitted">A submission (true) or a view (false).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task TouchAsync(EmailActionToken t, bool submitted, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(t);
            if (submitted) t.SubmitCount++;
            else t.OpenCount++;
            t.LastUsedUtc = DateTime.UtcNow;
            await _Database.EmailActionTokens.UpdateAsync(t, token).ConfigureAwait(false);
        }

        /// <summary>
        /// A random URL-safe secret (32 bytes).
        /// </summary>
        /// <returns>Secret.</returns>
        public static string NewSecret()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /// <summary>
        /// SHA-256 of a secret, lowercase hex.
        /// </summary>
        /// <param name="secret">Secret.</param>
        /// <returns>Hash.</returns>
        public static string Hash(string secret)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret ?? String.Empty))).ToLowerInvariant();
        }
    }
}
