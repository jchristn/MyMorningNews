namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Analytics reports. Defaults: the caller's data for the last 90 days.
    /// </summary>
    public class AnalyticsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AnalyticsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// Overview: topics, runs, findings, briefings, email, predictions, feedback, suggestions, and model cost.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The OverviewReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<OverviewReport> OverviewAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<OverviewReport>(HttpMethod.Get, "/v1.0/api/analytics/overview", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Relevance analytics.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RelevanceReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RelevanceReport> RelevanceAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<RelevanceReport>(HttpMethod.Get, "/v1.0/api/analytics/relevance", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Prediction accuracy and calibration.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The AccuracyReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<AccuracyReport> AccuracyAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<AccuracyReport>(HttpMethod.Get, "/v1.0/api/analytics/accuracy", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Feedback analytics.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The FeedbackReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<FeedbackReport> FeedbackAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<FeedbackReport>(HttpMethod.Get, "/v1.0/api/analytics/feedback", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Suggestion analytics.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SuggestionReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SuggestionReport> SuggestionsAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<SuggestionReport>(HttpMethod.Get, "/v1.0/api/analytics/suggestions", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Feedback reason analytics.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ReasonReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ReasonReport> ReasonsAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<ReasonReport>(HttpMethod.Get, "/v1.0/api/analytics/reasons", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Pipeline quality analytics.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The QualityReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<QualityReport> QualityAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<QualityReport>(HttpMethod.Get, "/v1.0/api/analytics/quality", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Email delivery and engagement analytics.
        /// </summary>
        /// <param name="options">Scope, or null for the caller's last 90 days.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailReport> EmailAsync(AnalyticsOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EmailReport>(HttpMethod.Get, "/v1.0/api/analytics/email", (options ?? new AnalyticsOptions()).ToQuery(), null, token);
        }
    }
}
