namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Verifies that a quote returned by a model appears verbatim in the stored source text (plan section 19). Matching
    /// ignores case, collapses whitespace, and treats typographic quotes, apostrophes, and dashes as their plain forms.
    /// Thread safety: thread safe.
    /// </summary>
    public static class QuoteVerifier
    {
        /// <summary>
        /// Shortest quote accepted as evidence, in characters after normalization. Default 12.
        /// </summary>
        public const int MinimumQuoteLength = 12;

        /// <summary>
        /// Look for a quote in a source text.
        /// </summary>
        /// <param name="source">Source text.</param>
        /// <param name="quote">Quote.</param>
        /// <returns>Result; Found is false when the quote is missing or too short.</returns>
        public static VerifiedQuote Verify(string? source, string? quote)
        {
            VerifiedQuote result = new VerifiedQuote();
            if (String.IsNullOrEmpty(source) || String.IsNullOrWhiteSpace(quote)) return result;
            List<int> map = new List<int>();
            string normSource = Normalize(source, map);
            string normQuote = Normalize(quote.Trim().Trim('"', '\'', '“', '”'), null);
            if (normQuote.Length < MinimumQuoteLength) return result;
            int at = normSource.IndexOf(normQuote, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                string trimmed = normQuote.TrimEnd('.', ',', ';', ':', '!', '?');
                if (trimmed.Length >= MinimumQuoteLength) at = normSource.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase);
                if (at < 0) return result;
                normQuote = trimmed;
            }

            result.Found = true;
            result.Start = map[at];
            result.End = map[at + normQuote.Length - 1] + 1;
            result.Text = source.Substring(result.Start, result.End - result.Start);
            result.Context = Paragraph(source, result.Start, result.End);
            return result;
        }

        /// <summary>
        /// A stable hash of a quote's normalized form.
        /// </summary>
        /// <param name="quote">Quote.</param>
        /// <returns>Lowercase hex SHA-256.</returns>
        public static string Hash(string quote)
        {
            string n = Normalize(quote ?? String.Empty, null).ToLowerInvariant();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n))).ToLowerInvariant();
        }

        private static string Normalize(string text, List<int>? map)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            bool space = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '‘': case '’': case '‚': case '′': c = '\''; break;
                    case '“': case '”': case '„': case '″': c = '"'; break;
                    case '–': case '—': case '−': c = '-'; break;
                    case ' ': c = ' '; break;
                    case '*': case '_': continue;
                }

                if (Char.IsWhiteSpace(c))
                {
                    if (space || sb.Length == 0) continue;
                    space = true;
                    sb.Append(' ');
                    map?.Add(i);
                    continue;
                }

                space = false;
                sb.Append(c);
                map?.Add(i);
            }

            if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            {
                sb.Length--;
                if (map != null && map.Count > 0) map.RemoveAt(map.Count - 1);
            }

            return sb.ToString();
        }

        private static string Paragraph(string source, int start, int end)
        {
            int from = source.LastIndexOf("\n\n", Math.Max(0, start - 1), StringComparison.Ordinal);
            from = from < 0 ? Math.Max(0, start - 300) : from + 2;
            if (start - from > 400) from = start - 400;
            int to = source.IndexOf("\n\n", end, StringComparison.Ordinal);
            to = to < 0 ? Math.Min(source.Length, end + 300) : to;
            if (to - end > 400) to = end + 400;
            return source.Substring(from, Math.Max(0, to - from)).Trim();
        }
    }
}
