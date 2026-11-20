namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using MyMorningNews.Benchmark.Reference;

    /// <summary>
    /// Checks a dataset against SCHEMA.md and its own internal consistency: unique ids, references that resolve, probe
    /// rounds that match item rounds, negative flags that match the probe type, quotable sentences of 3 to 40 words,
    /// blocked items on a blocked domain, and at least one negative probe per corpus.
    /// </summary>
    public static class DatasetValidator
    {
        #region Private-Members

        private static readonly Regex _IdPattern = new Regex("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);
        private static readonly HashSet<string> _Tiers = new HashSet<string>(StringComparer.Ordinal) { "live", "synthetic", "public", "local" };
        private static readonly HashSet<string> _Weights = new HashSet<string>(StringComparer.Ordinal) { "Exclude", "Low", "Normal", "High" };
        private static readonly HashSet<string> _Ratings = new HashSet<string>(StringComparer.Ordinal) { "Relevant", "SomewhatRelevant", "NotRelevant" };
        private static readonly HashSet<string> _QuoteModes = new HashSet<string>(StringComparer.Ordinal) { "verbatim", "typography", "fabricated", "fabricated-only", "altered", "short" };
        private static readonly HashSet<string> _Constraints = new HashSet<string>(StringComparer.Ordinal)
        {
            "IncludeKeyword", "ExcludeKeyword", "IncludeEntity", "ExcludeEntity", "PreferredDomain", "BlockedDomain", "TrackedQuestion", "FeedUrl", "SeedUrl"
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate a dataset.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <returns>Problems found; empty when valid.</returns>
        /// <exception cref="ArgumentNullException">dataset is null.</exception>
        public static List<string> Validate(BenchmarkDataset dataset)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            List<string> errors = new List<string>();
            if (String.IsNullOrWhiteSpace(dataset.Name)) errors.Add("dataset: name is required");
            if (!_Tiers.Contains(dataset.Tier)) errors.Add("dataset: tier must be live, synthetic, public, or local");
            if (dataset.Corpora.Count == 0) errors.Add("dataset: at least one corpus is required");
            foreach (IGrouping<string, BenchmarkCorpus> dup in dataset.Corpora.GroupBy(c => c.Id).Where(g => g.Count() > 1)) errors.Add("dataset: duplicate corpus id " + dup.Key);
            foreach (BenchmarkCorpus c in dataset.Corpora) ValidateCorpus(c, errors);
            return errors;
        }

        #endregion

        #region Private-Methods

        private static void ValidateCorpus(BenchmarkCorpus c, List<string> errors)
        {
            string p = "corpus " + c.Id + ": ";
            if (!_IdPattern.IsMatch(c.Id ?? String.Empty)) errors.Add(p + "id must be lowercase letters, digits, and dashes");
            if (c.Rounds < 1) errors.Add(p + "rounds must be at least 1");
            if (String.IsNullOrWhiteSpace(c.Topic.Name)) errors.Add(p + "topic name is required");
            if (String.IsNullOrWhiteSpace(c.Topic.Brief)) errors.Add(p + "topic brief is required");
            if (c.Topic.NoveltyPreference < 0 || c.Topic.NoveltyPreference > 200) errors.Add(p + "noveltyPreference must be 0 to 200");
            foreach (IGrouping<string, BenchmarkDimension> d in c.Topic.Dimensions.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) errors.Add(p + "duplicate dimension " + d.Key);
            foreach (BenchmarkDimension d in c.Topic.Dimensions.Where(d => !_Weights.Contains(d.Weight))) errors.Add(p + "dimension " + d.Name + " has an unknown weight " + d.Weight);
            foreach (BenchmarkConstraint k in c.Topic.Constraints.Where(k => !_Constraints.Contains(k.Kind) || String.IsNullOrWhiteSpace(k.Value))) errors.Add(p + "constraint " + k.Kind + "=" + k.Value + " is invalid");

            Dictionary<string, BenchmarkItem> items = new Dictionary<string, BenchmarkItem>(StringComparer.Ordinal);
            foreach (BenchmarkItem i in c.Items)
            {
                if (!_IdPattern.IsMatch(i.Id ?? String.Empty)) errors.Add(p + "item id '" + i.Id + "' must be lowercase letters, digits, and dashes");
                else if (!items.TryAdd(i.Id!, i)) errors.Add(p + "duplicate item id " + i.Id);
            }

            foreach (BenchmarkItem i in c.Items) ValidateItem(c, i, items, errors);
            HashSet<string> probeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BenchmarkProbe probe in c.Probes)
            {
                if (!probeIds.Add(probe.Id)) errors.Add(p + "duplicate probe id " + probe.Id);
                ValidateProbe(c, probe, items, errors);
            }

            if (!c.Probes.Any(x => x.Negative)) errors.Add(p + "at least one negative probe is required");
            foreach (BenchmarkFeedback f in c.Feedback)
            {
                if (!items.TryGetValue(f.Item, out BenchmarkItem? fi)) errors.Add(p + "feedback names unknown item " + f.Item);
                else if (!fi.Rounds.Contains(f.AfterRound)) errors.Add(p + "feedback item " + f.Item + " is not in round " + f.AfterRound);
                if (f.AfterRound < 1 || f.AfterRound >= c.Rounds) errors.Add(p + "feedback afterRound " + f.AfterRound + " must be before the last round");
                if (!_Ratings.Contains(f.Relevance)) errors.Add(p + "feedback relevance " + f.Relevance + " is unknown");
            }
        }

        private static void ValidateItem(BenchmarkCorpus c, BenchmarkItem i, Dictionary<string, BenchmarkItem> items, List<string> errors)
        {
            string p = "corpus " + c.Id + " item " + i.Id + ": ";
            if (i.Rounds.Count == 0) errors.Add(p + "rounds is empty");
            foreach (int r in i.Rounds.Where(r => r < 1 || r > c.Rounds)) errors.Add(p + "round " + r + " is outside 1.." + c.Rounds);
            if (i.Relevance < 0 || i.Relevance > 3) errors.Add(p + "relevance must be 0 to 3");
            if (i.HoursAgo < 0) errors.Add(p + "hoursAgo must not be negative");
            if (!_QuoteModes.Contains(i.Quotes)) errors.Add(p + "unknown quotes mode " + i.Quotes);
            if (i.AliasOf != null)
            {
                if (!items.TryGetValue(i.AliasOf, out BenchmarkItem? target)) errors.Add(p + "aliasOf names unknown item " + i.AliasOf);
                else if (target.AliasOf != null) errors.Add(p + "aliasOf must name an item that is not itself an alias");
                if (i.Paragraphs.Count > 0 || !String.IsNullOrEmpty(i.Title)) errors.Add(p + "an alias has no title or paragraphs of its own");
                return;
            }

            if (i.Url != null && !(Uri.TryCreate(i.Url, UriKind.Absolute, out Uri? u) && (u.Scheme == "http" || u.Scheme == "https"))) errors.Add(p + "url must be an absolute http or https URL");
            if (String.IsNullOrWhiteSpace(i.Title)) errors.Add(p + "title is required");
            if (i.Paragraphs.Count == 0) errors.Add(p + "at least one paragraph is required");
            if (i.Url != null) return;
            int quotable = i.Paragraphs.Count(par => TextTools.Sentences(par).Any(s => WordCount(s) >= 3 && WordCount(s) <= 40));
            int needed = i.Quotes == "verbatim" || i.Quotes == "typography" ? Math.Min(2, i.Paragraphs.Count) : 1;
            if (quotable < needed) errors.Add(p + "needs " + needed + " paragraphs whose first sentence is 3 to 40 words (evidence spans)");
            foreach (string par in i.Paragraphs.Take(2))
            {
                string first = TextTools.Sentences(par).FirstOrDefault() ?? String.Empty;
                int w = WordCount(first);
                if (w < 3 || w > 40) errors.Add(p + "first sentence of a quoted paragraph has " + w + " words; evidence spans must be 3 to 40 words");
            }
        }

        private static void ValidateProbe(BenchmarkCorpus c, BenchmarkProbe probe, Dictionary<string, BenchmarkItem> items, List<string> errors)
        {
            string p = "corpus " + c.Id + " probe " + probe.Id + ": ";
            if (!_IdPattern.IsMatch(probe.Id ?? String.Empty)) errors.Add(p + "id must be lowercase letters, digits, and dashes");
            if (!ProbeTypes.IsKnown(probe.Type)) { errors.Add(p + "unknown type " + probe.Type); return; }
            if (String.IsNullOrWhiteSpace(probe.Category)) errors.Add(p + "category is required");
            if (probe.Round < 1 || probe.Round > c.Rounds) errors.Add(p + "round " + probe.Round + " is outside 1.." + c.Rounds);
            bool shouldBeNegative = ProbeTypes.NegativeTypes.Contains(probe.Type);
            if (probe.Negative != shouldBeNegative) errors.Add(p + "negative must be " + shouldBeNegative.ToString().ToLowerInvariant() + " for type " + probe.Type);

            switch (probe.Type)
            {
                case ProbeTypes.Ranking:
                case ProbeTypes.Empty:
                case ProbeTypes.Guardrail:
                case ProbeTypes.DedupePrecision:
                    if (probe.Item != null || probe.Items.Count > 0) errors.Add(p + "a " + probe.Type + " probe names no items");
                    if (probe.Type == ProbeTypes.Empty && c.Items.Any(i => i.Rounds.Contains(probe.Round) && i.Relevance > 0)) errors.Add(p + "an empty probe's round must have no relevant items");
                    break;
                case ProbeTypes.Cluster:
                    if (probe.Items.Count < 2) errors.Add(p + "a cluster needs at least two items");
                    foreach (string id in probe.Items) RequireInRound(p, id, probe.Round, items, errors);
                    break;
                case ProbeTypes.Order:
                    RequireInRound(p, probe.Above, probe.Round, items, errors);
                    RequireInRound(p, probe.Below, probe.Round, items, errors);
                    break;
                default:
                    BenchmarkItem? subject = RequireInRound(p, probe.Item, probe.Round, items, errors);
                    if (subject == null) break;
                    if (probe.Type == ProbeTypes.Surfaced && subject.Relevance < 2) errors.Add(p + "a surfaced item needs relevance 2 or 3");
                    if (probe.Type == ProbeTypes.Suppressed && subject.Relevance != 0) errors.Add(p + "a suppressed item needs relevance 0");
                    if (probe.Type == ProbeTypes.Evidence && subject.Url != null) errors.Add(p + "an evidence item must be served by the stub");
                    if (probe.Type == ProbeTypes.Blocked && !IsBlocked(c, subject)) errors.Add(p + "a blocked item needs a url on a BlockedDomain constraint");
                    if (probe.Type == ProbeTypes.Canonical && (subject.AliasOf == null || subject.AliasOf != probe.Of)) errors.Add(p + "a canonical probe names an alias item and its original in of");
                    if (probe.Type == ProbeTypes.Seen && !subject.Rounds.Any(r => r < probe.Round)) errors.Add(p + "a seen item must also appear in an earlier round");
                    if (probe.Type == ProbeTypes.Duplicate || probe.Type == ProbeTypes.Update)
                    {
                        if (probe.Of == null || !items.TryGetValue(probe.Of, out BenchmarkItem? of)) errors.Add(p + "of must name an existing item");
                        else if (!of.Rounds.Any(r => r < probe.Round)) errors.Add(p + "the original (" + probe.Of + ") must appear in an earlier round");
                    }

                    break;
            }
        }

        private static BenchmarkItem? RequireInRound(string p, string? id, int round, Dictionary<string, BenchmarkItem> items, List<string> errors)
        {
            if (String.IsNullOrEmpty(id) || !items.TryGetValue(id, out BenchmarkItem? item))
            {
                errors.Add(p + "names unknown item '" + id + "'");
                return null;
            }

            if (!item.Rounds.Contains(round)) errors.Add(p + "item " + id + " is not in round " + round);
            return item;
        }

        private static bool IsBlocked(BenchmarkCorpus c, BenchmarkItem i)
        {
            if (i.Url == null || !Uri.TryCreate(i.Url, UriKind.Absolute, out Uri? u)) return false;
            return c.Topic.Constraints.Where(k => k.Kind == "BlockedDomain").Any(k => u.Host == k.Value || u.Host.EndsWith("." + k.Value, StringComparison.OrdinalIgnoreCase));
        }

        private static int WordCount(string s)
        {
            return s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        #endregion
    }
}
