namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Reference;

    /// <summary>
    /// The stub search engine: for a corpus round, returns the round's pages that share at least one word with the
    /// query, ordered by the number of shared words and then dataset order. Published dates are the item's age before
    /// the current time. The search engine is not the system under test, so it is simple and fully deterministic in
    /// its ordering. Thread safe.
    /// </summary>
    public class StubSearch
    {
        #region Private-Members

        private readonly DatasetIndex _Index;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="index">Dataset index.</param>
        /// <exception cref="ArgumentNullException">index is null.</exception>
        public StubSearch(DatasetIndex index)
        {
            _Index = index ?? throw new ArgumentNullException(nameof(index));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Items a query returns, best first.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="round">Round.</param>
        /// <param name="query">Query text.</param>
        /// <returns>Item ids.</returns>
        public List<string> Search(string corpusId, int round, string query)
        {
            HashSet<string> terms = new HashSet<string>(TextTools.Tokens(query), StringComparer.Ordinal);
            List<BenchmarkItem> items = _Index.ItemsInRound(corpusId, round);
            List<KeyValuePair<string, int>> scored = new List<KeyValuePair<string, int>>();
            foreach (BenchmarkItem i in items)
            {
                HashSet<string> words = new HashSet<string>(TextTools.Tokens(_Index.Text(corpusId, i.Id)), StringComparer.Ordinal);
                int overlap = terms.Count(words.Contains);
                if (overlap > 0) scored.Add(new KeyValuePair<string, int>(i.Id, overlap));
            }

            return scored.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        }

        /// <summary>
        /// A SearXNG JSON response for a query.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="round">Round.</param>
        /// <param name="query">Query text.</param>
        /// <param name="nowUtc">Current time, for published dates.</param>
        /// <returns>Response object to serialize.</returns>
        public object Response(string corpusId, int round, string query, DateTime nowUtc)
        {
            List<object> results = new List<object>();
            foreach (string id in Search(corpusId, round, query))
            {
                BenchmarkItem item = _Index.Item(corpusId, id)!;
                BenchmarkItem content = _Index.ContentItem(corpusId, id) ?? item;
                string snippet = content.Paragraphs.Count == 0 ? String.Empty : TextTools.Sentences(content.Paragraphs[0]).FirstOrDefault() ?? String.Empty;
                results.Add(new
                {
                    url = _Index.Url(corpusId, id),
                    title = content.Title,
                    content = snippet,
                    publishedDate = nowUtc.AddHours(-content.HoursAgo).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    engine = "bench",
                    score = 1.0
                });
            }

            return new { query, number_of_results = results.Count, results };
        }

        #endregion
    }
}
