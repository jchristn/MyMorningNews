namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The probe types the quality command scores, and which of them are negative (the correct outcome is "nothing").
    /// </summary>
    public static class ProbeTypes
    {
        /// <summary>NDCG@10 of the round's ranked findings against graded item relevance.</summary>
        public const string Ranking = "ranking";

        /// <summary>A relevant item yields a shown, non-duplicate finding.</summary>
        public const string Surfaced = "surfaced";

        /// <summary>An irrelevant item yields no shown finding (negative).</summary>
        public const string Suppressed = "suppressed";

        /// <summary>A repeat of an earlier round's story is flagged as a duplicate.</summary>
        public const string Duplicate = "duplicate";

        /// <summary>Of a same-round duplicate cluster, exactly one item yields a shown finding.</summary>
        public const string Cluster = "cluster";

        /// <summary>A story with a material new fact is linked as an update of the earlier finding.</summary>
        public const string Update = "update";

        /// <summary>A result on a blocked domain is never fetched and yields nothing (negative).</summary>
        public const string Blocked = "blocked";

        /// <summary>A page fetched in an earlier round is skipped as seen (negative).</summary>
        public const string Seen = "seen";

        /// <summary>A tracking-parameter variant of a page in the same round is skipped as a duplicate URL (negative).</summary>
        public const string Canonical = "canonical";

        /// <summary>Quotes are verified when present in the page and rejected when not.</summary>
        public const string Evidence = "evidence";

        /// <summary>A page carrying injected instructions yields no off-task output (negative).</summary>
        public const string Injection = "injection";

        /// <summary>One item ranks above another (feedback and preference effects).</summary>
        public const string Order = "order";

        /// <summary>A round in which nothing is relevant shows no findings (negative).</summary>
        public const string Empty = "empty";

        /// <summary>Every model request in the corpus carries the scope guardrail.</summary>
        public const string Guardrail = "guardrail";

        /// <summary>Flagged duplicates are true duplicates (dedupe precision), per round.</summary>
        public const string DedupePrecision = "dedupe-precision";

        /// <summary>
        /// Every known type.
        /// </summary>
        public static readonly IReadOnlyList<string> All = new List<string>
        {
            Ranking, Surfaced, Suppressed, Duplicate, Cluster, Update, Blocked, Seen, Canonical, Evidence, Injection, Order, Empty, Guardrail, DedupePrecision
        };

        /// <summary>
        /// Types that are always negative probes.
        /// </summary>
        public static readonly IReadOnlyList<string> NegativeTypes = new List<string> { Suppressed, Blocked, Seen, Canonical, Injection, Empty };

        /// <summary>
        /// Whether a type is known.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>True when known.</returns>
        public static bool IsKnown(string type)
        {
            return All.Any(t => String.Equals(t, type, StringComparison.Ordinal));
        }
    }
}
