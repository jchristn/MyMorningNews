namespace MyMorningNews.Benchmark.Reference
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// Text helpers shared by the stub model and the reference arms: a tokenizer with a light plural stemmer, feature-hashed
    /// bag-of-words vectors (texts that share words get similar vectors, so similarity stays meaningful), cosine and
    /// Jaccard similarity, sentence splitting, and the quote normalization used to check evidence. Thread safe.
    /// </summary>
    public static class TextTools
    {
        #region Private-Members

        private static readonly HashSet<string> _Stopwords = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "an", "and", "are", "as", "at", "be", "been", "but", "by", "can", "for", "from", "has", "have", "he", "her", "his", "how", "if", "in", "into",
            "is", "it", "its", "of", "on", "or", "our", "she", "so", "than", "that", "the", "their", "them", "then", "there", "these", "they", "this", "to",
            "track", "was", "we", "were", "what", "when", "which", "who", "will", "with", "would", "you", "your", "about", "after", "also", "any", "all",
            "more", "most", "new", "not", "now", "one", "said", "says", "such", "two", "up", "out", "over", "per", "do", "does", "did", "who", "why", "where"
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Lowercase word tokens of two or more characters, without stopwords, with a trailing plural "s" removed from
        /// words longer than four characters.
        /// </summary>
        /// <param name="text">Text, or null.</param>
        /// <returns>Tokens in order.</returns>
        public static List<string> Tokens(string? text)
        {
            List<string> tokens = new List<string>();
            if (String.IsNullOrEmpty(text)) return tokens;
            StringBuilder current = new StringBuilder();
            foreach (char ch in text.ToLowerInvariant())
            {
                if (Char.IsLetterOrDigit(ch)) current.Append(ch);
                else Flush(current, tokens);
            }

            Flush(current, tokens);
            return tokens;
        }

        /// <summary>
        /// A feature-hashed, L2-normalized bag-of-words vector.
        /// </summary>
        /// <param name="text">Text, or null.</param>
        /// <param name="dimensions">Dimensions, 8 to 4096.</param>
        /// <returns>Vector (all zeros for text with no tokens).</returns>
        public static float[] Vector(string? text, int dimensions)
        {
            int dims = Math.Clamp(dimensions, 8, 4096);
            float[] v = new float[dims];
            foreach (string t in Tokens(text))
            {
                uint h = StableHash.Fnv1a(t);
                int index = (int)(h % (uint)dims);
                float sign = ((h >> 31) & 1) == 0 ? 1f : -1f;
                v[index] += sign;
            }

            double norm = Math.Sqrt(v.Sum(x => (double)x * x));
            if (norm > 0) for (int i = 0; i < dims; i++) v[i] = (float)(v[i] / norm);
            return v;
        }

        /// <summary>
        /// Cosine similarity of two vectors.
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        /// <returns>Similarity, -1 to 1; 0 when lengths differ or a vector is all zeros.</returns>
        public static double Cosine(float[] a, float[] b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            if (a.Length != b.Length || a.Length == 0) return 0;
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }

            return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        /// <summary>
        /// Cosine similarity of two texts' 512-dimension vectors.
        /// </summary>
        /// <param name="a">First text.</param>
        /// <param name="b">Second text.</param>
        /// <returns>Similarity.</returns>
        public static double Similarity(string? a, string? b)
        {
            return Cosine(Vector(a, 512), Vector(b, 512));
        }

        /// <summary>
        /// Jaccard similarity of the token sets of two texts.
        /// </summary>
        /// <param name="a">First text.</param>
        /// <param name="b">Second text.</param>
        /// <returns>Similarity, 0 to 1.</returns>
        public static double Jaccard(string? a, string? b)
        {
            HashSet<string> sa = new HashSet<string>(Tokens(a), StringComparer.Ordinal);
            HashSet<string> sb = new HashSet<string>(Tokens(b), StringComparer.Ordinal);
            if (sa.Count == 0 || sb.Count == 0) return 0;
            int inter = sa.Count(sb.Contains);
            return (double)inter / (sa.Count + sb.Count - inter);
        }

        /// <summary>
        /// Tokens that contain a digit and appear in the first text but not in the second (new numbers, dates, amounts).
        /// </summary>
        /// <param name="candidate">Candidate text.</param>
        /// <param name="existing">Existing text.</param>
        /// <returns>New numeric tokens.</returns>
        public static List<string> NewNumbers(string? candidate, string? existing)
        {
            HashSet<string> old = new HashSet<string>(Tokens(existing), StringComparer.Ordinal);
            return Tokens(candidate).Where(t => t.Any(Char.IsDigit) && !old.Contains(t)).Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Split a paragraph into sentences at ". ", "? ", and "! " (keeping the punctuation).
        /// </summary>
        /// <param name="paragraph">Paragraph, or null.</param>
        /// <returns>Sentences.</returns>
        public static List<string> Sentences(string? paragraph)
        {
            List<string> result = new List<string>();
            if (String.IsNullOrWhiteSpace(paragraph)) return result;
            int start = 0;
            for (int i = 0; i < paragraph.Length - 1; i++)
            {
                char c = paragraph[i];
                if ((c == '.' || c == '?' || c == '!') && paragraph[i + 1] == ' ')
                {
                    result.Add(paragraph.Substring(start, i + 1 - start).Trim());
                    start = i + 2;
                }
            }

            if (start < paragraph.Length) result.Add(paragraph.Substring(start).Trim());
            return result.Where(s => s.Length > 0).ToList();
        }

        /// <summary>
        /// Normalize text for quote matching: lowercase, straight quotes and dashes, no Markdown emphasis, single spaces.
        /// </summary>
        /// <param name="text">Text, or null.</param>
        /// <returns>Normalized text.</returns>
        public static string NormalizeQuote(string? text)
        {
            if (String.IsNullOrEmpty(text)) return String.Empty;
            StringBuilder sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char raw in text.ToLowerInvariant())
            {
                char c = raw switch
                {
                    '\u2018' or '\u2019' or '\u201B' or '\u2032' => '\'',
                    '\u201C' or '\u201D' or '\u201F' or '\u2033' => '"',
                    '\u2013' or '\u2014' or '\u2212' => '-',
                    '\u00A0' => ' ',
                    _ => raw
                };
                if (c == '*' || c == '_' || c == '`') continue;
                if (Char.IsWhiteSpace(c))
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space) sb.Append(' ');
                space = false;
                sb.Append(c);
            }

            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        private static void Flush(StringBuilder current, List<string> tokens)
        {
            if (current.Length == 0) return;
            string t = current.ToString();
            current.Clear();
            if (t.Length < 2 || _Stopwords.Contains(t)) return;
            if (t.Length > 4 && t.EndsWith('s') && !t.EndsWith("ss", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);
            tokens.Add(t);
        }

        #endregion
    }
}
