namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Converts a safe Markdown subset to HTML: headings, paragraphs, bullet and numbered lists, block quotes, rules,
    /// fenced code, bold, italic, inline code, and links. All text is HTML encoded exactly once and raw HTML in the
    /// input is shown as text, so model and user content cannot inject markup. Links allow http, https, and mailto only.
    /// Thread safety: stateless.
    /// </summary>
    public static class SafeMarkdown
    {
        private static readonly Regex _Code = new Regex(@"`([^`\n]+)`", RegexOptions.Compiled);
        private static readonly Regex _Link = new Regex(@"\[([^\[\]\n]+)\]\(([^()\s]+)\)", RegexOptions.Compiled);
        private static readonly Regex _Auto = new Regex(@"&lt;((?:https?://|mailto:)[^\s&]+(?:&amp;[^\s&]+)*)&gt;", RegexOptions.Compiled);
        private static readonly Regex _Bold = new Regex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*|__(?=\S)(.+?)(?<=\S)__", RegexOptions.Compiled);
        private static readonly Regex _Italic = new Regex(@"(?<![*\w])\*(?=\S)(.+?)(?<=\S)\*(?![*\w])|(?<![_\w])_(?=\S)(.+?)(?<=\S)_(?![_\w])", RegexOptions.Compiled);
        private static readonly Regex _Ordered = new Regex(@"^\s{0,3}(\d{1,3})[.)]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex _Bullet = new Regex(@"^\s{0,3}[-*+]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex _Heading = new Regex(@"^(#{1,6})\s+(.*?)\s*#*\s*$", RegexOptions.Compiled);
        private static readonly Regex _Rule = new Regex(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Render Markdown to an HTML fragment.
        /// </summary>
        /// <param name="markdown">Markdown; null renders empty.</param>
        /// <param name="headingOffset">Levels to add to every heading (0 to 5), so a document's # can become h2.</param>
        /// <returns>HTML fragment.</returns>
        public static string ToHtml(string? markdown, int headingOffset = 0)
        {
            string[] lines = (markdown ?? String.Empty).Replace("\r\n", "\n").Split('\n');
            StringBuilder sb = new StringBuilder();
            List<string> para = new List<string>();
            string? listKind = null;
            bool inCode = false;
            StringBuilder code = new StringBuilder();
            List<string> quote = new List<string>();
            int offset = Math.Clamp(headingOffset, 0, 5);

            void FlushPara()
            {
                if (para.Count == 0) return;
                sb.Append("<p>").Append(Inline(String.Join(" ", para))).Append("</p>\n");
                para.Clear();
            }

            void FlushList()
            {
                if (listKind == null) return;
                sb.Append("</").Append(listKind).Append(">\n");
                listKind = null;
            }

            void FlushQuote()
            {
                if (quote.Count == 0) return;
                sb.Append("<blockquote>").Append(ToHtml(String.Join("\n", quote), offset)).Append("</blockquote>\n");
                quote.Clear();
            }

            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (inCode)
                {
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        sb.Append("<pre><code>").Append(WebUtility.HtmlEncode(code.ToString().TrimEnd('\n'))).Append("</code></pre>\n");
                        code.Clear();
                        inCode = false;
                    }
                    else
                    {
                        code.Append(raw).Append('\n');
                    }

                    continue;
                }

                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    FlushPara(); FlushList(); FlushQuote();
                    inCode = true;
                    continue;
                }

                if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    FlushPara(); FlushList();
                    string q = line.TrimStart().Substring(1);
                    quote.Add(q.StartsWith(" ", StringComparison.Ordinal) ? q.Substring(1) : q);
                    continue;
                }

                FlushQuote();
                if (line.Trim().Length == 0)
                {
                    FlushPara(); FlushList();
                    continue;
                }

                Match h = _Heading.Match(line);
                if (h.Success)
                {
                    FlushPara(); FlushList();
                    int level = Math.Min(6, h.Groups[1].Value.Length + offset);
                    sb.Append("<h").Append(level).Append('>').Append(Inline(h.Groups[2].Value)).Append("</h").Append(level).Append(">\n");
                    continue;
                }

                if (_Rule.IsMatch(line))
                {
                    FlushPara(); FlushList();
                    sb.Append("<hr>\n");
                    continue;
                }

                Match b = _Bullet.Match(line);
                Match o = b.Success ? Match.Empty : _Ordered.Match(line);
                if (b.Success || o.Success)
                {
                    FlushPara();
                    string kind = b.Success ? "ul" : "ol";
                    if (listKind != kind)
                    {
                        FlushList();
                        listKind = kind;
                        sb.Append('<').Append(kind);
                        if (kind == "ol" && o.Groups[1].Value != "1") sb.Append(" start=\"").Append(int.Parse(o.Groups[1].Value)).Append('"');
                        sb.Append(">\n");
                    }

                    sb.Append("<li>").Append(Inline(b.Success ? b.Groups[1].Value : o.Groups[2].Value)).Append("</li>\n");
                    continue;
                }

                if (listKind != null && raw.StartsWith("  ", StringComparison.Ordinal))
                {
                    int close = sb.ToString().LastIndexOf("</li>", StringComparison.Ordinal);
                    if (close >= 0) sb.Insert(close, " " + Inline(line.Trim()));
                    continue;
                }

                FlushList();
                para.Add(line.Trim());
            }

            if (inCode) sb.Append("<pre><code>").Append(WebUtility.HtmlEncode(code.ToString().TrimEnd('\n'))).Append("</code></pre>\n");
            FlushPara(); FlushList(); FlushQuote();
            return sb.ToString();
        }

        /// <summary>
        /// Render inline Markdown (bold, italic, code, links) to HTML. Text is encoded.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>HTML.</returns>
        public static string Inline(string? text)
        {
            string s = WebUtility.HtmlEncode(text ?? String.Empty);
            List<string> slots = new List<string>();
            string Hold(string html)
            {
                slots.Add(html);
                return "\u0001" + (slots.Count - 1) + "\u0002";
            }

            s = _Code.Replace(s, m => Hold("<code>" + m.Groups[1].Value + "</code>"));
            s = _Link.Replace(s, m =>
            {
                string url = WebUtility.HtmlDecode(m.Groups[2].Value);
                if (!IsSafeUrl(url)) return m.Groups[1].Value;
                return Hold("<a href=\"" + WebUtility.HtmlEncode(url) + "\">" + m.Groups[1].Value + "</a>");
            });
            s = _Auto.Replace(s, m =>
            {
                string url = WebUtility.HtmlDecode(m.Groups[1].Value);
                return IsSafeUrl(url) ? Hold("<a href=\"" + WebUtility.HtmlEncode(url) + "\">" + WebUtility.HtmlEncode(url) + "</a>") : m.Value;
            });
            s = _Bold.Replace(s, m => "<strong>" + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) + "</strong>");
            s = _Italic.Replace(s, m => "<em>" + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) + "</em>");
            for (int i = slots.Count - 1; i >= 0; i--) s = s.Replace("\u0001" + i + "\u0002", slots[i], StringComparison.Ordinal);
            return s;
        }

        /// <summary>
        /// Plain text from Markdown: markup removed, links reduced to their text. Used for word counts and text email.
        /// </summary>
        /// <param name="markdown">Markdown.</param>
        /// <returns>Text.</returns>
        public static string ToText(string? markdown)
        {
            string s = (markdown ?? String.Empty).Replace("\r\n", "\n");
            s = Regex.Replace(s, @"^```.*$", String.Empty, RegexOptions.Multiline);
            s = _Link.Replace(s, "$1 ($2)");
            s = Regex.Replace(s, @"^#{1,6}\s+", String.Empty, RegexOptions.Multiline);
            s = Regex.Replace(s, @"(\*\*|__|`)", String.Empty);
            s = Regex.Replace(s, @"(?<![\w*])\*(?=\S)(.+?)(?<=\S)\*(?![\w*])", "$1");
            s = Regex.Replace(s, @"^\s*>\s?", String.Empty, RegexOptions.Multiline);
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        /// <summary>
        /// Whether a URL is safe to link: absolute http, https, or mailto.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>True when safe.</returns>
        public static bool IsSafeUrl(string? url)
        {
            if (String.IsNullOrWhiteSpace(url)) return false;
            if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return url.Length > 7 && !url.Contains('<') && !url.Contains('>');
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
        }
    }
}
