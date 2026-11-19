namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Spoken scripts: parse a model script into chapters and speaker lines, write a script from briefing Markdown when no
    /// model is available, clean text for speech (no URLs, citation markers, or Markdown), and split it into segments under
    /// the speech endpoint limit at sentence boundaries. Thread safety: stateless.
    /// </summary>
    public static class SpeechScript
    {
        private static readonly Regex _Speaker = new Regex(@"^\s*\**(HOST_A|HOST_B)\**\s*:\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _Url = new Regex(@"https?://\S+", RegexOptions.Compiled);
        private static readonly Regex _Citation = new Regex(@"\s*\[(\d+(?:\s*,\s*\d+)*)\]", RegexOptions.Compiled);
        private static readonly Regex _Link = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex _Sentence = new Regex(@"(?<=[.!?])\s+(?=[A-Z0-9""'(])", RegexOptions.Compiled);

        /// <summary>
        /// Parse a script. "## Heading" lines start chapters and are not spoken; HOST_A: and HOST_B: prefix speaker lines.
        /// </summary>
        /// <param name="script">Script text.</param>
        /// <returns>Parts with at least one line each.</returns>
        public static List<ScriptPart> Parse(string? script)
        {
            List<ScriptPart> parts = new List<ScriptPart>();
            ScriptPart current = new ScriptPart { Heading = "Introduction" };
            string? speaker = null;
            foreach (string raw in (script ?? String.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (current.Lines.Count > 0) parts.Add(current);
                    current = new ScriptPart { Heading = Clean(line.TrimStart('#').Trim()) };
                    continue;
                }

                Match m = _Speaker.Match(line);
                if (m.Success)
                {
                    speaker = m.Groups[1].Value.ToUpperInvariant();
                    line = m.Groups[2].Value;
                }

                string text = Clean(line);
                if (text.Length == 0) continue;
                ScriptLine? last = current.Lines.LastOrDefault();
                if (!m.Success && last != null && last.Speaker == speaker) last.Text += " " + text;
                else current.Lines.Add(new ScriptLine { Speaker = speaker, Text = text });
            }

            if (current.Lines.Count > 0) parts.Add(current);
            return parts;
        }

        /// <summary>
        /// Write a script from briefing Markdown without a model: the intro, then each section as a chapter with a spoken
        /// heading. Two-host scripts alternate paragraphs between the hosts. The Sources section is left out.
        /// </summary>
        /// <param name="markdown">Briefing Markdown.</param>
        /// <param name="format">narrator or two-host.</param>
        /// <param name="intro">Spoken intro, or null.</param>
        /// <returns>Script text.</returns>
        public static string FromBriefing(string? markdown, string format, string? intro)
        {
            bool twoHost = String.Equals(format, "two-host", StringComparison.OrdinalIgnoreCase);
            StringBuilder sb = new StringBuilder();
            int turn = 0;
            string Prefix()
            {
                if (!twoHost) return String.Empty;
                return (turn++ % 2 == 0 ? "HOST_A" : "HOST_B") + ": ";
            }

            sb.Append("## Introduction\n");
            if (!String.IsNullOrWhiteSpace(intro)) sb.Append(Prefix()).Append(intro.Trim()).Append('\n');
            bool skip = false;
            foreach (string block in Blocks(markdown))
            {
                if (block.StartsWith("# ", StringComparison.Ordinal))
                {
                    sb.Append(Prefix()).Append(block.Substring(2).Trim()).Append(".\n");
                    continue;
                }

                if (block.StartsWith("## ", StringComparison.Ordinal))
                {
                    string heading = block.Substring(3).Trim();
                    skip = String.Equals(heading, "Sources", StringComparison.OrdinalIgnoreCase) || String.Equals(heading, "References", StringComparison.OrdinalIgnoreCase);
                    if (skip) continue;
                    sb.Append("## ").Append(heading).Append('\n');
                    sb.Append(Prefix()).Append("Next: ").Append(heading).Append(".\n");
                    continue;
                }

                if (skip) continue;
                if (block.StartsWith("### ", StringComparison.Ordinal))
                {
                    sb.Append(Prefix()).Append(block.Substring(4).Trim()).Append(".\n");
                    continue;
                }

                sb.Append(Prefix()).Append(block).Append('\n');
            }

            sb.Append(Prefix()).Append("That is the briefing. Thanks for listening.\n");
            return sb.ToString();
        }

        /// <summary>
        /// Make text speakable: Markdown links become their text, URLs and citation markers are removed, emphasis and
        /// list markers are dropped, and whitespace is collapsed.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Speakable text.</returns>
        public static string Clean(string? text)
        {
            string s = text ?? String.Empty;
            s = _Link.Replace(s, "$1");
            s = _Url.Replace(s, String.Empty);
            s = _Citation.Replace(s, String.Empty);
            s = Regex.Replace(s, @"^\s*(?:[-*+]|\d+[.)])\s+", String.Empty);
            s = s.Replace("**", String.Empty).Replace("__", String.Empty).Replace("`", String.Empty).Replace("*", String.Empty);
            s = Regex.Replace(s, @"(?<!\w)_(\S.*?\S)_(?!\w)", "$1");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        /// <summary>
        /// Split parts into segments of one speaker each, no longer than maxCharacters, breaking at sentence boundaries
        /// (and at word boundaries for a single overlong sentence).
        /// </summary>
        /// <param name="parts">Parts.</param>
        /// <param name="maxCharacters">Most characters per segment (minimum 50).</param>
        /// <returns>Segments in order.</returns>
        public static List<SpeechSegment> Segment(IReadOnlyList<ScriptPart> parts, int maxCharacters)
        {
            ArgumentNullException.ThrowIfNull(parts);
            int max = Math.Max(50, maxCharacters);
            List<SpeechSegment> segments = new List<SpeechSegment>();
            for (int p = 0; p < parts.Count; p++)
            {
                SpeechSegment? open = null;
                foreach (ScriptLine line in parts[p].Lines)
                {
                    foreach (string sentence in Sentences(line.Text, max))
                    {
                        if (open != null && open.Speaker == line.Speaker && open.Text.Length + 1 + sentence.Length <= max)
                        {
                            open.Text += " " + sentence;
                            continue;
                        }

                        open = new SpeechSegment { PartIndex = p, Speaker = line.Speaker, Text = sentence };
                        segments.Add(open);
                    }
                }
            }

            return segments;
        }

        private static IEnumerable<string> Sentences(string text, int max)
        {
            foreach (string s in _Sentence.Split(text).Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                if (s.Length <= max)
                {
                    yield return s;
                    continue;
                }

                StringBuilder chunk = new StringBuilder();
                foreach (string word in s.Split(' '))
                {
                    if (chunk.Length > 0 && chunk.Length + 1 + word.Length > max)
                    {
                        yield return chunk.ToString();
                        chunk.Clear();
                    }

                    if (chunk.Length > 0) chunk.Append(' ');
                    chunk.Append(word.Length > max ? word.Substring(0, max) : word);
                }

                if (chunk.Length > 0) yield return chunk.ToString();
            }
        }

        private static IEnumerable<string> Blocks(string? markdown)
        {
            StringBuilder para = new StringBuilder();
            foreach (string raw in (markdown ?? String.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    if (para.Length > 0) { yield return Clean(para.ToString()); para.Clear(); }
                    string level = new string(line.TakeWhile(c => c == '#').ToArray());
                    yield return (level.Length >= 3 ? "### " : level + " ") + Clean(line.TrimStart('#'));
                    continue;
                }

                if (line.Length == 0 || Regex.IsMatch(line, @"^(?:[-*+]|\d+[.)])\s+"))
                {
                    if (para.Length > 0) { yield return Clean(para.ToString()); para.Clear(); }
                    if (line.Length > 0)
                    {
                        string item = Clean(line);
                        if (item.Length > 0) yield return item.EndsWith(".", StringComparison.Ordinal) || item.EndsWith("?", StringComparison.Ordinal) || item.EndsWith("!", StringComparison.Ordinal) ? item : item + ".";
                    }

                    continue;
                }

                if (para.Length > 0) para.Append(' ');
                para.Append(line);
            }

            if (para.Length > 0) yield return Clean(para.ToString());
        }
    }
}
