namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Research runs. There is no event stream route; WaitAsync polls until a run finishes.
    /// </summary>
    public class RunsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public RunsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List runs.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Run items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Run>> ListAsync(RunListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Run>>(HttpMethod.Get, "/v1.0/api/runs", (options ?? new RunListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a run.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Run.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Run> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Run>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// A run's stages in order.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of RunStage items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<RunStage>> ListStagesAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<List<RunStage>>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/stages", null, null, token);
        }

        /// <summary>
        /// A run's search queries.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of SearchQuery items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<SearchQuery>> ListQueriesAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<List<SearchQuery>>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/queries", null, null, token);
        }

        /// <summary>
        /// A run's search results with dispositions.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="disposition">Only this disposition, or null for all.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of SearchResult items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<SearchResult>> ListResultsAsync(string id, SearchResultDispositionEnum? disposition = null, CancellationToken token = default)
        {
            return Client.SendAsync<List<SearchResult>>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/results", Query("disposition", disposition), null, token);
        }

        /// <summary>
        /// Render the run digest email without sending it.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EmailPreview.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EmailPreview> GetEmailPreviewAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<EmailPreview>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/email-preview", null, null, token);
        }

        /// <summary>
        /// Cancel a queued or running run.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Run.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Run> CancelAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Run>(HttpMethod.Post, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/cancel", null, null, token);
        }

        /// <summary>
        /// A run's model calls, oldest first (payloads are left out).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of LlmCall items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<LlmCall>> ListLlmCallsAsync(string id, ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<LlmCall>>(HttpMethod.Get, "/v1.0/api/runs/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/llm-calls", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Whether a run status is terminal (Completed, PartiallySucceeded, Failed, Cancelled, or Skipped).
        /// </summary>
        /// <param name="status">Status.</param>
        /// <returns>True when the run will not change again.</returns>
        public static bool IsTerminal(RunStatusEnum status)
        {
            return status != RunStatusEnum.Queued && status != RunStatusEnum.Running;
        }

        /// <summary>
        /// Poll a run until it reaches a terminal status. The server has no run event stream, so this reads GET /runs/{id}
        /// every pollInterval.
        /// </summary>
        /// <param name="runId">Run identifier.</param>
        /// <param name="timeout">Maximum time to wait. Null waits 30 minutes.</param>
        /// <param name="pollInterval">Delay between reads. Null uses 2 seconds. Minimum 100 milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The run in its terminal status.</returns>
        /// <exception cref="ArgumentNullException">runId is null or empty.</exception>
        /// <exception cref="TimeoutException">The run did not finish in time.</exception>
        /// <exception cref="MyMorningNewsException">The request failed.</exception>
        public async Task<Run> WaitAsync(string runId, TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentNullException(nameof(runId));
            TimeSpan limit = timeout ?? TimeSpan.FromMinutes(30);
            TimeSpan delay = pollInterval ?? TimeSpan.FromSeconds(2);
            if (delay < TimeSpan.FromMilliseconds(100)) delay = TimeSpan.FromMilliseconds(100);
            DateTime deadline = DateTime.UtcNow + limit;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Run run = await GetAsync(runId, token).ConfigureAwait(false);
                if (IsTerminal(run.Status)) return run;
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("Run " + runId + " is still " + run.Status + " after " + limit.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds.");
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
    }
}
