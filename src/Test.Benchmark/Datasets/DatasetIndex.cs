namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// URL and content lookups for a dataset as served by the stub at a base URL. The stub, the scorer, and the
    /// reference arms all use it, so they agree on which page is which. Thread safe after construction.
    /// </summary>
    public class DatasetIndex
    {
        #region Public-Members

        /// <summary>
        /// Tracking parameters appended to an alias item's URL.
        /// </summary>
        public const string AliasQuery = "?utm_source=newsletter&utm_medium=email&utm_campaign=bench";

        /// <summary>
        /// The dataset.
        /// </summary>
        public BenchmarkDataset Dataset { get; }

        /// <summary>
        /// Stub base URL, such as http://127.0.0.1:12503.
        /// </summary>
        public string BaseUrl { get; }

        #endregion

        #region Private-Members

        private readonly Dictionary<string, BenchmarkCorpus> _Corpora = new Dictionary<string, BenchmarkCorpus>(StringComparer.Ordinal);
        private readonly Dictionary<string, BenchmarkItem> _Items = new Dictionary<string, BenchmarkItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _Urls = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _ByUrl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Index a dataset.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <param name="baseUrl">Stub base URL without a trailing slash.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public DatasetIndex(BenchmarkDataset dataset, string baseUrl)
        {
            Dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            ArgumentNullException.ThrowIfNull(baseUrl);
            BaseUrl = baseUrl.TrimEnd('/');
            foreach (BenchmarkCorpus c in dataset.Corpora)
            {
                _Corpora[c.Id] = c;
                foreach (BenchmarkItem i in c.Items) _Items[Key(c.Id, i.Id)] = i;
            }

            foreach (BenchmarkCorpus c in dataset.Corpora)
            {
                foreach (BenchmarkItem i in c.Items)
                {
                    string url = BuildUrl(c, i);
                    _Urls[Key(c.Id, i.Id)] = url;
                    _ByUrl[Key(c.Id, url)] = i.Id;
                }
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// A corpus by id.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <returns>Corpus, or null.</returns>
        public BenchmarkCorpus? Corpus(string corpusId)
        {
            return _Corpora.TryGetValue(corpusId, out BenchmarkCorpus? c) ? c : null;
        }

        /// <summary>
        /// An item by id.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="itemId">Item.</param>
        /// <returns>Item, or null.</returns>
        public BenchmarkItem? Item(string corpusId, string itemId)
        {
            return _Items.TryGetValue(Key(corpusId, itemId), out BenchmarkItem? i) ? i : null;
        }

        /// <summary>
        /// The item whose content a page shows (the original for an alias).
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="itemId">Item.</param>
        /// <returns>Content item, or null.</returns>
        public BenchmarkItem? ContentItem(string corpusId, string itemId)
        {
            BenchmarkItem? i = Item(corpusId, itemId);
            if (i?.AliasOf != null) return Item(corpusId, i.AliasOf);
            return i;
        }

        /// <summary>
        /// The URL search returns for an item.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="itemId">Item.</param>
        /// <returns>URL, or null for an unknown item.</returns>
        public string? Url(string corpusId, string itemId)
        {
            return _Urls.TryGetValue(Key(corpusId, itemId), out string? u) ? u : null;
        }

        /// <summary>
        /// The item behind a URL: an exact match first, then the same URL without its query string.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="url">URL, or null.</param>
        /// <returns>Item id, or null.</returns>
        public string? ItemForUrl(string corpusId, string? url)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;
            if (_ByUrl.TryGetValue(Key(corpusId, url), out string? id)) return id;
            string bare = Bare(url);
            return _ByUrl.TryGetValue(Key(corpusId, bare), out string? id2) ? id2 : null;
        }

        /// <summary>
        /// Locate a page served by the stub from a request path such as /corpus/heat-pumps/hp-01.html.
        /// </summary>
        /// <param name="path">Request path.</param>
        /// <param name="corpusId">Corpus, when found.</param>
        /// <param name="itemId">Content item, when found.</param>
        /// <returns>True when the path names a served page.</returns>
        public bool TryPage(string path, out string corpusId, out string itemId)
        {
            corpusId = String.Empty;
            itemId = String.Empty;
            string[] parts = (path ?? String.Empty).Trim('/').Split('/');
            if (parts.Length != 3 || parts[0] != "corpus" || !parts[2].EndsWith(".html", StringComparison.Ordinal)) return false;
            string item = parts[2].Substring(0, parts[2].Length - 5);
            BenchmarkItem? i = ContentItem(parts[1], item);
            if (i == null || i.Url != null) return false;
            corpusId = parts[1];
            itemId = i.Id;
            return true;
        }

        /// <summary>
        /// The text of an item as the page shows it: title, then paragraphs, separated by blank lines.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="itemId">Item.</param>
        /// <returns>Text, or an empty string.</returns>
        public string Text(string corpusId, string itemId)
        {
            BenchmarkItem? i = ContentItem(corpusId, itemId);
            if (i == null) return String.Empty;
            return i.Title + "\n\n" + String.Join("\n\n", i.Paragraphs);
        }

        /// <summary>
        /// Items search returns in a round, in dataset order.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="round">Round.</param>
        /// <returns>Items.</returns>
        public List<BenchmarkItem> ItemsInRound(string corpusId, int round)
        {
            BenchmarkCorpus? c = Corpus(corpusId);
            return c == null ? new List<BenchmarkItem>() : c.Items.Where(i => i.Rounds.Contains(round)).ToList();
        }

        /// <summary>
        /// The search provider endpoint for a corpus round.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="round">Round.</param>
        /// <returns>Endpoint URL.</returns>
        public string SearchEndpoint(string corpusId, int round)
        {
            return BaseUrl + "/search/" + corpusId + "/r" + round;
        }

        #endregion

        #region Private-Methods

        private string BuildUrl(BenchmarkCorpus c, BenchmarkItem i)
        {
            if (i.AliasOf != null)
            {
                BenchmarkItem? original = Item(c.Id, i.AliasOf);
                string baseUrl = original == null ? BaseUrl + "/corpus/" + c.Id + "/" + i.AliasOf + ".html" : BuildUrl(c, original);
                return baseUrl + AliasQuery;
            }

            return i.Url ?? BaseUrl + "/corpus/" + c.Id + "/" + i.Id + ".html";
        }

        private static string Bare(string url)
        {
            int q = url.IndexOfAny(new[] { '?', '#' });
            return q < 0 ? url : url.Substring(0, q);
        }

        private static string Key(string a, string b)
        {
            return a + "\u001f" + b;
        }

        #endregion
    }
}
