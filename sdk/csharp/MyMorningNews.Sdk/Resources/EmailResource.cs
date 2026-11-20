namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Email outbox, test email, run digest previews, and the caller's email preferences.
    /// </summary>
    public class EmailResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public EmailResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List the email outbox (rendered bodies are left out).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of EmailMessage items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<EmailMessage>> ListAsync(EmailMessageListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<EmailMessage>>(HttpMethod.Get, "/v1.0/api/email-messages", (options ?? new EmailMessageListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read an email with its rendered HTML and text.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailMessage.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailMessage> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<EmailMessage>(HttpMethod.Get, "/v1.0/api/email-messages/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Queue an email again (HTTP 202).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailMessage.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailMessage> ResendAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<EmailMessage>(HttpMethod.Post, "/v1.0/api/email-messages/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/resend", null, null, token);
        }

        /// <summary>
        /// Delete an email from the outbox.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/email-messages/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Send a test email to the caller (HTTP 202 with the queued message).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailMessage.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailMessage> SendTestAsync(CancellationToken token = default)
        {
            return Client.SendAsync<EmailMessage>(HttpMethod.Post, "/v1.0/api/email/test", null, null, token);
        }

        /// <summary>
        /// Render a run digest without sending it.
        /// </summary>
        /// <param name="runId">Run identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailPreview.</returns>
        /// <exception cref="ArgumentNullException">runId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailPreview> PreviewRunAsync(string runId, CancellationToken token = default)
        {
            return Client.SendAsync<EmailPreview>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(runId, nameof(runId)) + "/email-preview", null, null, token);
        }

        /// <summary>
        /// The caller's email preferences.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailPreferencesResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailPreferencesResponse> GetPreferencesAsync(CancellationToken token = default)
        {
            return Client.SendAsync<EmailPreferencesResponse>(HttpMethod.Get, "/v1.0/api/email-preferences", null, null, token);
        }

        /// <summary>
        /// Change the caller's email preferences.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailPreferencesResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailPreferencesResponse> UpdatePreferencesAsync(EmailPreferencesRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<EmailPreferencesResponse>(HttpMethod.Put, "/v1.0/api/email-preferences", null, request, token);
        }
    }
}
