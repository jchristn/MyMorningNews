namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// The caller's account: plan, promotional codes, usage meter, data export, and deletion. Also the public (anonymous)
    /// sign-up, email verification, and password reset routes, which never send the client's credentials.
    /// Thread safety: stateless; safe to share.
    /// </summary>
    public class AccountResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AccountResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// The caller's plan and its limits.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EffectivePlan.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EffectivePlan> GetPlanAsync(CancellationToken token = default)
        {
            return Client.SendAsync<EffectivePlan>(HttpMethod.Get, "/v1.0/api/account/plan", null, null, token);
        }

        /// <summary>
        /// Redeem a promotional code (409 PromoCodeInvalid with details.reason).
        /// </summary>
        /// <param name="code">Promotional code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EffectivePlan.</returns>
        /// <exception cref="ArgumentNullException">code is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EffectivePlan> RedeemAsync(string code, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(code)) throw new ArgumentNullException(nameof(code));
            return Client.SendAsync<EffectivePlan>(HttpMethod.Post, "/v1.0/api/promo-codes/redeem", null, new PromoRedeemRequest { Code = code }, token);
        }

        /// <summary>
        /// Check a promotional code without redeeming it (public).
        /// </summary>
        /// <param name="code">Promotional code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromoCheck.</returns>
        /// <exception cref="ArgumentNullException">code is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromoCheck> CheckPromoCodeAsync(string code, CancellationToken token = default)
        {
            return Client.SendAsync<PromoCheck>(HttpMethod.Get, "/v1.0/public/promo-codes/" + MyMorningNewsClient.Segment(code, nameof(code)), null, null, token);
        }

        /// <summary>
        /// The caller's usage against the plan's limits (month-to-date cost, topics, runs today, audio minutes). A null
        /// limit means unlimited. Needs a signed-in user, not an access key (400 otherwise).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UsageMeter.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UsageMeter> GetUsageAsync(CancellationToken token = default)
        {
            return Client.SendAsync<UsageMeter>(HttpMethod.Get, "/v1.0/api/account/usage", null, null, token);
        }

        /// <summary>
        /// Download the caller's data as a ZIP of JSON and Markdown (profile, topics, briefings, and so on). Needs a signed-in
        /// user, not an access key.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ZIP file, buffered in DownloadResult.Data.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> ExportAsync(CancellationToken token = default)
        {
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/account/export", null, null, null, null, token);
        }

        /// <summary>
        /// Download the caller's data export (ZIP) into a stream.
        /// </summary>
        /// <param name="destination">Stream to write to.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The download result (the bytes went to destination).</returns>
        /// <exception cref="ArgumentNullException">destination is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DownloadResult> ExportToAsync(Stream destination, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return Client.DownloadAsync(HttpMethod.Get, "/v1.0/api/account/export", null, null, null, destination, token);
        }

        /// <summary>
        /// Delete the caller's account. Signs out every session and schedules the purge after the server's grace period
        /// (Hosting.DeletionGraceDays, default 7 days); signing in again before then cancels it. Administrators and
        /// protected accounts are refused with 409.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The AccountDeletionResponse with PurgeAfterUtc.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<AccountDeletionResponse> DeleteAsync(CancellationToken token = default)
        {
            return Client.SendAsync<AccountDeletionResponse>(HttpMethod.Post, "/v1.0/api/account/delete", null, null, token);
        }

        /// <summary>
        /// Sign up for the hosted service (public, no credentials). Creates a personal tenant and an account, then emails a
        /// verification link when the server requires one. Always 202 with the same body, whether or not the email already
        /// has an account. 404 SignupClosed when sign-up is off (Hosting.PublicSignupEnabled), 400 for a weak password,
        /// missing terms or age confirmation, or a blocked domain, and 429 when one address signs up too often.
        /// </summary>
        /// <param name="request">Sign-up request (Email, Password, AcceptTerms, and ConfirmAge are required).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SignupResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SignupResponse> SignUpAsync(SignupRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<SignupResponse>(HttpMethod.Post, "/v1.0/public/signup", null, request, token, false);
        }

        /// <summary>
        /// Confirm an email address with the token from the emailed link (public). A link works once; 400 when it is not
        /// valid, already used, or expired.
        /// </summary>
        /// <param name="verificationToken">Token from the link's token query value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailVerifiedResponse.</returns>
        /// <exception cref="ArgumentNullException">verificationToken is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailVerifiedResponse> VerifyEmailAsync(string verificationToken, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(verificationToken)) throw new ArgumentNullException(nameof(verificationToken));
            return Client.SendAsync<EmailVerifiedResponse>(HttpMethod.Post, "/v1.0/public/verify-email", null, new AccountTokenRequest { Token = verificationToken }, token, false);
        }

        /// <summary>
        /// Send a new verification link to an unconfirmed sign-up account (public). Always 202.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The AccountMessageResponse.</returns>
        /// <exception cref="ArgumentNullException">email is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<AccountMessageResponse> ResendVerificationAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(email)) throw new ArgumentNullException(nameof(email));
            return Client.SendAsync<AccountMessageResponse>(HttpMethod.Post, "/v1.0/public/verify-email/resend", null, new AccountEmailRequest { Email = email }, token, false);
        }

        /// <summary>
        /// Email a password reset link (public). Always 202.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The AccountMessageResponse.</returns>
        /// <exception cref="ArgumentNullException">email is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<AccountMessageResponse> ForgotPasswordAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(email)) throw new ArgumentNullException(nameof(email));
            return Client.SendAsync<AccountMessageResponse>(HttpMethod.Post, "/v1.0/public/password/forgot", null, new AccountEmailRequest { Email = email }, token, false);
        }

        /// <summary>
        /// Set a new password with the token from a reset link (public). Signs out every session and emails a confirmation.
        /// 400 when the link is not valid, already used, or expired, or the password is too weak (minimum 8 characters).
        /// </summary>
        /// <param name="resetToken">Token from the link's token query value.</param>
        /// <param name="password">New password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PasswordResetResponse.</returns>
        /// <exception cref="ArgumentNullException">resetToken or password is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PasswordResetResponse> ResetPasswordAsync(string resetToken, string password, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(resetToken)) throw new ArgumentNullException(nameof(resetToken));
            if (String.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            return Client.SendAsync<PasswordResetResponse>(HttpMethod.Post, "/v1.0/public/password/reset", null, new AccountTokenRequest { Token = resetToken, Password = password }, token, false);
        }

        /// <summary>
        /// The caller's custom topics against the plan's topic limit, in keep order. Topics with WithinPlan false keep
        /// their history but do not run. Needs a signed-in user.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicSlotsResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicSlotsResponse> GetTopicSlotsAsync(CancellationToken token = default)
        {
            return Client.SendAsync<TopicSlotsResponse>(HttpMethod.Get, "/v1.0/api/account/topic-slots", null, null, token);
        }

        /// <summary>
        /// Choose which custom topics to keep running first when the plan allows fewer than the caller has (most
        /// important first). An empty list clears the choice. Nothing is paused, archived, or deleted.
        /// </summary>
        /// <param name="request">Ordered topic identifiers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicSlotsResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error (400 ValidationFailed for a duplicate, unknown, archived, one-time, or another user's topic).</exception>
        public Task<TopicSlotsResponse> SetTopicSlotsAsync(TopicSlotsRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TopicSlotsResponse>(HttpMethod.Put, "/v1.0/api/account/topic-slots", null, request, token);
        }

        /// <summary>
        /// Clear the caller's topic keep order so topics follow creation order.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicSlotsResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicSlotsResponse> ClearTopicSlotsAsync(CancellationToken token = default)
        {
            return SetTopicSlotsAsync(new TopicSlotsRequest(), token);
        }
    }
}
