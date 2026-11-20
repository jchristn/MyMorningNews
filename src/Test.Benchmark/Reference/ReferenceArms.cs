namespace MyMorningNews.Benchmark.Reference
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Runners;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// The two anchors computed by the harness over the same data, without the product. floor-search-order is the task
    /// with no product at all: every page the searches return, in search order, shown as-is (no blocking, no URL
    /// canonicalization, no memory of earlier rounds, no dedupe). reference-lexical is a plain, well-understood pipeline
    /// under the same constraints as the product: blocked domains dropped, query strings stripped, pages seen in earlier
    /// rounds skipped, the same stub judgment for relevance with a fixed threshold of 40, and word-overlap dedupe (Jaccard
    /// of title and lead at least 0.5) with update links when a repeat carries a new number. Thread safe.
    /// </summary>
    public class ReferenceArms
    {
        #region Public-Members

        /// <summary>
        /// Queries per round, matching the product's default MaxQueriesPerRun. Default 8.
        /// </summary>
        public int MaxQueries { get; set; } = 8;

        /// <summary>
        /// Results kept per query, matching the product's default MaxResultsPerQuery. Default 10.
        /// </summary>
        public int MaxResultsPerQuery { get; set; } = 10;

        /// <summary>
        /// Display threshold for the lexical reference. Default 40 (the product default).
        /// </summary>
        public int Threshold { get; set; } = 40;

        #endregion

        #region Private-Members

        private readonly DatasetIndex _Index;
        private readonly StubSearch _Search;
        private readonly StubModelBrain _Brain;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="index">Dataset index.</param>
        /// <param name="brain">Stub model, for its relevance judgment.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ReferenceArms(DatasetIndex index, StubModelBrain brain)
        {
            _Index = index ?? throw new ArgumentNullException(nameof(index));
            _Brain = brain ?? throw new ArgumentNullException(nameof(brain));
            _Search = new StubSearch(index);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build every round of an arm.
        /// </summary>
        /// <param name="arm">floor-search-order or reference-lexical.</param>
        /// <param name="corpus">Corpus.</param>
        /// <returns>Round data by round.</returns>
        /// <exception cref="ArgumentException">Unknown arm.</exception>
        public Dictionary<int, RoundData> Build(string arm, BenchmarkCorpus corpus)
        {
            ArgumentNullException.ThrowIfNull(corpus);
            if (arm != "floor-search-order" && arm != "reference-lexical") throw new ArgumentException("Unknown reference arm " + arm + ".");
            bool lexical = arm == "reference-lexical";
            Dictionary<int, RoundData> rounds = new Dictionary<int, RoundData>();
            HashSet<string> seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<FindingDto> history = new List<FindingDto>();
            Dictionary<string, string> historyText = new Dictionary<string, string>(StringComparer.Ordinal);
            List<string> dims = corpus.Topic.Dimensions.Where(d => d.Weight != "Exclude").Select(d => d.Name).ToList();
            List<KeyValuePair<string, string>> plan = corpus.Topic.Dimensions.Where(d => d.Weight != "Exclude").Select(d => new KeyValuePair<string, string>(d.Name, d.Description ?? String.Empty)).ToList();
            string topicText = corpus.Topic.Name + " " + corpus.Topic.Brief + " " + String.Join(" ", dims);
            List<string> blocked = corpus.Topic.Constraints.Where(k => k.Kind == "BlockedDomain").Select(k => k.Value).ToList();

            for (int round = 1; round <= corpus.Rounds; round++)
            {
                RoundData data = new RoundData { Round = round };
                List<string> ordered = new List<string>();
                foreach (string q in StubModelBrain.PlanQueries(corpus.Topic.Name, plan, MaxQueries))
                    foreach (string id in _Search.Search(corpus.Id, round, q).Take(MaxResultsPerQuery))
                        if (!ordered.Contains(id)) ordered.Add(id);

                HashSet<string> inRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<KeyValuePair<FindingDto, string>> candidates = new List<KeyValuePair<FindingDto, string>>();
                int rank = 0;
                foreach (string id in ordered)
                {
                    string url = _Index.Url(corpus.Id, id)!;
                    string content = _Index.Item(corpus.Id, id)?.AliasOf ?? id;
                    SearchResultDto result = new SearchResultDto { Id = "res-" + round + "-" + id, Url = url, Rank = ++rank, Disposition = "Fetched", SourceItemId = "src-" + content };
                    data.Results.Add(result);
                    data.ItemByResult[result.Id] = id;
                    if (lexical)
                    {
                        string bare = Bare(url);
                        if (blocked.Any(b => Host(url) == b || Host(url).EndsWith("." + b, StringComparison.OrdinalIgnoreCase))) { result.Disposition = "SkippedBlocked"; continue; }
                        if (!inRun.Add(bare)) { result.Disposition = "SkippedDuplicate"; continue; }
                        if (seenUrls.Contains(bare)) { result.Disposition = "SkippedSeen"; continue; }
                    }

                    BenchmarkItem item = _Index.ContentItem(corpus.Id, id)!;
                    string lead = String.Join(" ", item.Paragraphs.Take(2).Select(p => TextTools.Sentences(p).FirstOrDefault() ?? p));
                    double score = 50;
                    if (lexical)
                    {
                        if (StubModelBrain.Relevance(item.Title + " " + String.Join(" ", item.Paragraphs), topicText) < _Brain.ExtractFloor) continue;
                        score = _Brain.ScoreFor(StubModelBrain.Relevance(item.Title + " " + lead, topicText));
                    }

                    FindingDto f = new FindingDto { Id = "ref-" + arm + "-r" + round + "-" + id, RunId = "ref-r" + round, SourceItemId = "src-" + content, Title = item.Title, Summary = lead, RelevanceScore = score, LlmScore = score, Tier = "Fyi" };
                    data.ItemByFinding[f.Id] = content;
                    candidates.Add(new KeyValuePair<FindingDto, string>(f, item.Title + " " + lead));
                }

                if (lexical)
                {
                    List<KeyValuePair<FindingDto, string>> kept = new List<KeyValuePair<FindingDto, string>>();
                    foreach (KeyValuePair<FindingDto, string> c in candidates.OrderByDescending(c => c.Key.RelevanceScore).ThenBy(c => c.Key.Title, StringComparer.Ordinal))
                    {
                        FindingDto f = c.Key;
                        if (f.RelevanceScore < Threshold) f.Tier = "BelowThreshold";
                        KeyValuePair<FindingDto, string> inRunMatch = kept.FirstOrDefault(k => TextTools.Jaccard(k.Value, c.Value) >= 0.5);
                        FindingDto? prior = history.Where(h => TextTools.Jaccard(historyText[h.Id], c.Value) >= 0.25).OrderByDescending(h => TextTools.Jaccard(historyText[h.Id], c.Value)).FirstOrDefault();
                        if (inRunMatch.Key != null) { f.IsDuplicate = true; f.DuplicateOfFindingId = inRunMatch.Key.Id; }
                        else if (prior != null && TextTools.NewNumbers(c.Value, historyText[prior.Id]).Count > 0) f.IsUpdateOfFindingId = prior.Id;
                        else if (prior != null && TextTools.Jaccard(historyText[prior.Id], c.Value) >= 0.5) { f.IsDuplicate = true; f.DuplicateOfFindingId = prior.Id; }
                        data.Findings.Add(f);
                        if (!f.IsDuplicate) kept.Add(c);
                    }

                    int r = 0;
                    foreach (FindingDto f in data.Findings.Where(f => f.Shown)) f.Rank = ++r;
                    foreach (KeyValuePair<FindingDto, string> k in kept) { history.Add(k.Key); historyText[k.Key.Id] = k.Value; }
                    foreach (SearchResultDto res in data.Results.Where(x => x.Disposition == "Fetched")) seenUrls.Add(Bare(res.Url));
                }
                else
                {
                    int r = 0;
                    foreach (KeyValuePair<FindingDto, string> c in candidates) { c.Key.Rank = ++r; data.Findings.Add(c.Key); }
                }

                rounds[round] = data;
            }

            return rounds;
        }

        #endregion

        #region Private-Methods

        private static string Bare(string url)
        {
            int q = url.IndexOfAny(new[] { '?', '#' });
            return q < 0 ? url : url.Substring(0, q);
        }

        private static string Host(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? u) ? u.Host : String.Empty;
        }

        #endregion
    }
}
