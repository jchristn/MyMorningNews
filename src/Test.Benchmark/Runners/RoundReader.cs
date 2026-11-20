namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;

    /// <summary>
    /// Reads back what a round produced over the API and maps it to dataset items through search result URLs.
    /// </summary>
    public static class RoundReader
    {
        #region Public-Methods

        /// <summary>
        /// Read a round.
        /// </summary>
        /// <param name="client">Reader's client.</param>
        /// <param name="index">Dataset index.</param>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="round">Round.</param>
        /// <param name="runId">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Round data.</returns>
        public static async Task<RoundData> ReadAsync(MyMorningNewsClient client, DatasetIndex index, string corpusId, int round, string runId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(index);
            RoundData data = new RoundData { Round = round };
            data.Run = await client.GetRunAsync(runId, token).ConfigureAwait(false);
            data.Results = await client.GetResultsAsync(runId, token).ConfigureAwait(false);
            data.Findings = await client.GetRunFindingsAsync(runId, token).ConfigureAwait(false);
            foreach (SearchResultDto r in data.Results)
            {
                string? item = index.ItemForUrl(corpusId, r.Url);
                if (item != null) data.ItemByResult[r.Id] = item;
            }

            foreach (FindingDto f in data.Findings)
            {
                if (f.SourceItemId == null) continue;
                SearchResultDto? r = data.Results.Where(x => x.SourceItemId == f.SourceItemId).OrderBy(x => x.Disposition == "Fetched" ? 0 : 1).FirstOrDefault();
                string? item = r != null && data.ItemByResult.TryGetValue(r.Id, out string? i) ? i : null;
                if (item != null) data.ItemByFinding[f.Id] = index.Item(corpusId, item)?.AliasOf ?? item;
                FindingDetailDto detail = await client.GetFindingAsync(f.Id, token).ConfigureAwait(false);
                data.Evidence[f.Id] = detail.Evidence.Select(e => e.Evidence).ToList();
            }

            return data;
        }

        #endregion
    }
}
