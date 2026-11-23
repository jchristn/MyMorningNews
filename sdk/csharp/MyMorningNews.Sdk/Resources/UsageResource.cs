namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Model usage and cost.
    /// </summary>
    public class UsageResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public UsageResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// The caller's own usage and cost (grouped by task unless GroupBy is set).
        /// </summary>
        /// <param name="options">Filters, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UsageReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UsageReport> GetMineAsync(UsageOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<UsageReport>(HttpMethod.Get, "/v1.0/api/usage/me", (options ?? new UsageOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Usage and cost report. Grouping by Model keys each row by endpoint and model name: Key is the endpoint identifier and the
        /// model name joined by a vertical bar, and ModelEndpointId and Model carry the two parts.
        /// </summary>
        /// <param name="options">Filters, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UsageReport.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UsageReport> GetReportAsync(UsageOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<UsageReport>(HttpMethod.Get, "/v1.0/api/usage/report", (options ?? new UsageOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Usage and cost time series. Every bucket carries calls, failures, cost, token types (input, output, cached, cache-write,
        /// reasoning), tool calls, and the average and 95th percentile time to first token. Set GroupBy on the options (any grouping
        /// except Day; Tenant needs AllTenants) to also get Groups on the series and Rows on each bucket; Top (server default 10)
        /// limits the groups and the rest are combined under the key (other).
        /// </summary>
        /// <param name="options">Filters, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UsageTimeseries.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UsageTimeseries> GetTimeseriesAsync(UsageOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<UsageTimeseries>(HttpMethod.Get, "/v1.0/api/usage/timeseries", (options ?? new UsageOptions()).ToQuery(), null, token);
        }
    }
}
