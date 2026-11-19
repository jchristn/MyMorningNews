namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Parses and validates briefing templates: front matter, a # title, optional opening instruction, and ## sections with
    /// mmn:section directives (plan section 21.1). Never throws on bad input; problems are returned with line numbers.
    /// Thread safety: stateless.
    /// </summary>
    public static class TemplateParser
    {
        /// <summary>
        /// Variables a title or instruction may use.
        /// </summary>
        public static readonly IReadOnlyList<string> Variables = new List<string> { "edition.name", "date.long", "date.short", "topic.name", "reader.name", "period.start", "period.end" };

        /// <summary>
        /// Directive sources.
        /// </summary>
        public static readonly IReadOnlyList<string> Sources = new List<string> { "findings", "predictions", "suggestions", "citations", "run-summaries", "none" };

        /// <summary>
        /// Most sections a template may have.
        /// </summary>
        public const int MaxSections = 20;

        private static readonly Regex _Directive = new Regex(@"<!--\s*mmn:section\b(?<attrs>[^>]*?)-->", RegexOptions.Compiled);
        private static readonly Regex _Attr = new Regex(@"(?<k>[A-Za-z-]+)\s*=\s*(?:""(?<v>[^""]*)""|(?<v>[^\s""]+))", RegexOptions.Compiled);
        private static readonly Regex _Variable = new Regex(@"\{\{\s*(?<n>[A-Za-z0-9_.]+)\s*\}\}", RegexOptions.Compiled);
        private static readonly string[] _Tiers = { "MustRead", "WorthALook", "Fyi" };
        private static readonly string[] _Groups = { "topic", "dimension", "category" };
        private static readonly string[] _Orders = { "relevance", "recency", "priority", "confidence", "horizon" };
        private static readonly string[] _PredictionStatuses = { "new", "due", "open", "resolved" };
        private static readonly string[] _SuggestionStatuses = { "New", "Accepted", "Snoozed", "Dismissed", "Done" };

        /// <summary>
        /// Parse a template. Check <see cref="BriefingTemplateDefinition.Problems"/> for errors before using it.
        /// </summary>
        /// <param name="content">Template Markdown.</param>
        /// <returns>Definition with problems.</returns>
        public static BriefingTemplateDefinition Parse(string? content)
        {
            BriefingTemplateDefinition def = new BriefingTemplateDefinition();
            string[] lines = (content ?? String.Empty).Replace("\r\n", "\n").Split('\n');
            int i = 0;
            if (lines.Length == 0 || String.IsNullOrWhiteSpace(content))
            {
                Error(def, 0, "The template is empty.");
                return def;
            }

            if (lines[0].Trim() == "---")
            {
                int end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
                if (end < 0)
                {
                    Error(def, 1, "Front matter starts with --- but never ends with ---.");
                    return def;
                }

                ParseFrontMatter(def, lines, 1, end);
                i = end + 1;
            }
            else
            {
                Warn(def, 1, "No front matter; defaults are used for tone, length, audience, and citations.");
            }

            bool titleSeen = false;
            StringBuilder opening = new StringBuilder();
            TemplateSection? current = null;
            StringBuilder instruction = new StringBuilder();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            for (; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                if (trimmed.StartsWith("# ", StringComparison.Ordinal) && current == null && !titleSeen)
                {
                    def.Title = trimmed.Substring(2).Trim();
                    titleSeen = true;
                    CheckVariables(def, i + 1, def.Title);
                    continue;
                }

                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (current != null) Finish(def, current, instruction);
                    instruction.Clear();
                    string heading = trimmed.Substring(3).Trim();
                    current = new TemplateSection { Heading = heading, Key = UniqueKey(Slug(heading), keys), Line = i + 1 };
                    if (heading.Length == 0) Error(def, i + 1, "A section heading is empty.");
                    continue;
                }

                Match m = _Directive.Match(line);
                if (m.Success)
                {
                    if (current == null)
                    {
                        Error(def, i + 1, "A directive must come after a ## section heading.");
                        continue;
                    }

                    ParseDirective(def, current, m.Groups["attrs"].Value, i + 1);
                    string rest = _Directive.Replace(line, String.Empty);
                    if (!String.IsNullOrWhiteSpace(rest)) instruction.AppendLine(rest);
                    continue;
                }

                if (trimmed.StartsWith("<!--", StringComparison.Ordinal) && trimmed.Contains("mmn:", StringComparison.Ordinal))
                {
                    Error(def, i + 1, "Unrecognized directive; use <!-- mmn:section source=... -->.");
                    continue;
                }

                if (current == null)
                {
                    if (!String.IsNullOrWhiteSpace(line)) opening.AppendLine(line);
                    CheckVariables(def, i + 1, line);
                }
                else
                {
                    instruction.AppendLine(line);
                    CheckVariables(def, i + 1, line);
                }
            }

            if (current != null) Finish(def, current, instruction);
            def.OpeningInstruction = opening.ToString().Trim();
            if (!titleSeen) Warn(def, 0, "No # title line; the default title is used.");
            if (def.Sections.Count == 0) Error(def, 0, "The template has no ## sections.");
            if (def.Sections.Count > MaxSections) Error(def, def.Sections[MaxSections].Line, "Templates can have at most " + MaxSections + " sections.");
            if (def.Sections.Count(s => s.Directive.Source == "citations") > 1) Warn(def, 0, "More than one citations section; sources will be listed more than once.");
            return def;
        }

        /// <summary>
        /// Whether a parsed template has errors (warnings are allowed).
        /// </summary>
        /// <param name="def">Definition.</param>
        /// <returns>True when there are errors.</returns>
        public static bool HasErrors(BriefingTemplateDefinition def)
        {
            ArgumentNullException.ThrowIfNull(def);
            return def.Problems.Any(p => !p.IsWarning);
        }

        /// <summary>
        /// Replace known variables in text; unknown ones are left as written.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="values">Values by variable name.</param>
        /// <returns>Text with variables replaced.</returns>
        public static string Substitute(string text, IReadOnlyDictionary<string, string> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return _Variable.Replace(text ?? String.Empty, m => values.TryGetValue(m.Groups["n"].Value, out string? v) ? v : m.Value);
        }

        /// <summary>
        /// Lowercase, dash-separated key for a heading.
        /// </summary>
        /// <param name="heading">Heading.</param>
        /// <returns>Key (section when nothing is left).</returns>
        public static string Slug(string heading)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in (heading ?? String.Empty).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }

            string s = sb.ToString().Trim('-');
            return s.Length == 0 ? "section" : (s.Length > 100 ? s.Substring(0, 100) : s);
        }

        private static void ParseFrontMatter(BriefingTemplateDefinition def, string[] lines, int start, int end)
        {
            bool inAudio = false;
            for (int i = start; i < end; i++)
            {
                string raw = StripComment(lines[i]);
                if (String.IsNullOrWhiteSpace(raw)) continue;
                bool indented = raw.StartsWith(" ", StringComparison.Ordinal) || raw.StartsWith("\t", StringComparison.Ordinal);
                int colon = raw.IndexOf(':');
                if (colon <= 0)
                {
                    Error(def, i + 1, "Expected key: value.");
                    continue;
                }

                string key = raw.Substring(0, colon).Trim();
                string value = Unquote(raw.Substring(colon + 1).Trim());
                if (indented && inAudio)
                {
                    switch (key)
                    {
                        case "enabled":
                            if (Bool(value, out bool en)) def.Audio.Enabled = en; else Error(def, i + 1, "audio.enabled must be true or false.");
                            break;
                        case "format":
                            if (value == "narrator" || value == "two-host") def.Audio.Format = value; else Error(def, i + 1, "audio.format must be narrator or two-host.");
                            break;
                        case "targetMinutes":
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tm) && tm >= 1 && tm <= 60) def.Audio.TargetMinutes = tm;
                            else Error(def, i + 1, "audio.targetMinutes must be a whole number from 1 to 60.");
                            break;
                        case "intro":
                            def.Audio.Intro = value.Length == 0 ? null : value;
                            break;
                        default:
                            Error(def, i + 1, "Unknown audio option '" + key + "'. Known: enabled, format, targetMinutes, intro.");
                            break;
                    }

                    continue;
                }

                if (indented)
                {
                    Error(def, i + 1, "Unexpected indentation; only the audio block has nested options.");
                    continue;
                }

                inAudio = false;
                switch (key)
                {
                    case "name":
                        if (value.Length == 0) Error(def, i + 1, "name cannot be empty."); else def.Name = value;
                        break;
                    case "description":
                        def.Description = value.Length == 0 ? null : value;
                        break;
                    case "tone":
                        if (value.Length > 0) def.Tone = value;
                        break;
                    case "length":
                        if (value == "short" || value == "medium" || value == "long") def.Length = value; else Error(def, i + 1, "length must be short, medium, or long.");
                        break;
                    case "audience":
                        if (value.Length > 0) def.Audience = value;
                        break;
                    case "citations":
                        if (value == "numbered" || value == "inline" || value == "none") def.Citations = value; else Error(def, i + 1, "citations must be numbered, inline, or none.");
                        break;
                    case "audio":
                        inAudio = true;
                        if (value.Length > 0) Error(def, i + 1, "audio takes nested options on the following lines.");
                        break;
                    default:
                        Error(def, i + 1, "Unknown option '" + key + "'. Known: name, description, tone, length, audience, citations, audio.");
                        break;
                }
            }
        }

        private static void ParseDirective(BriefingTemplateDefinition def, TemplateSection section, string attrs, int line)
        {
            SectionDirective d = section.Directive;
            string leftover = _Attr.Replace(attrs, String.Empty).Trim();
            if (leftover.Length > 0) Error(def, line, "Could not read '" + leftover + "' in the directive; write attributes as name=value.");
            foreach (Match a in _Attr.Matches(attrs))
            {
                string k = a.Groups["k"].Value;
                string v = a.Groups["v"].Value;
                switch (k)
                {
                    case "source":
                        if (Sources.Contains(v)) d.Source = v; else Error(def, line, "source must be one of " + String.Join(", ", Sources) + ".");
                        break;
                    case "tier":
                        string? tier = _Tiers.FirstOrDefault(t => String.Equals(t, v, StringComparison.OrdinalIgnoreCase));
                        if (tier != null) d.Tier = tier; else Error(def, line, "tier must be MustRead, WorthALook, or Fyi.");
                        break;
                    case "status":
                        d.Status = v;
                        break;
                    case "group":
                        if (_Groups.Contains(v)) d.Group = v; else Error(def, line, "group must be topic, dimension, or category.");
                        break;
                    case "limit":
                        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lim) && lim >= 1 && lim <= 25) d.Limit = lim;
                        else Error(def, line, "limit must be a whole number from 1 to 25.");
                        break;
                    case "order":
                        if (_Orders.Contains(v)) d.Order = v; else Error(def, line, "order must be one of " + String.Join(", ", _Orders) + ".");
                        break;
                    case "topic":
                        d.Topic = v;
                        break;
                    case "dimension":
                        d.Dimension = v;
                        break;
                    default:
                        Error(def, line, "Unknown directive attribute '" + k + "'. Known: source, tier, status, group, limit, order, topic, dimension.");
                        break;
                }
            }

            if (d.Status != null)
            {
                if (d.Source == "predictions" && !_PredictionStatuses.Contains(d.Status)) Error(def, line, "For predictions, status must be new, due, open, or resolved.");
                else if (d.Source == "suggestions" && !_SuggestionStatuses.Contains(d.Status)) Error(def, line, "For suggestions, status must be " + String.Join(", ", _SuggestionStatuses) + ".");
                else if (d.Source != "predictions" && d.Source != "suggestions") Warn(def, line, "status only applies to predictions and suggestions.");
            }

            if (d.Tier != null && d.Source != "findings") Warn(def, line, "tier only applies to findings.");
        }

        private static void Finish(BriefingTemplateDefinition def, TemplateSection section, StringBuilder instruction)
        {
            section.Instruction = instruction.ToString().Trim();
            if (section.Directive.Source == "none" && section.Instruction.Length == 0) Warn(def, section.Line, "Section '" + section.Heading + "' has no directive and no instruction.");
            def.Sections.Add(section);
        }

        private static void CheckVariables(BriefingTemplateDefinition def, int line, string text)
        {
            foreach (Match m in _Variable.Matches(text ?? String.Empty))
                if (!Variables.Contains(m.Groups["n"].Value)) Error(def, line, "Unknown variable {{" + m.Groups["n"].Value + "}}. Known: " + String.Join(", ", Variables.Select(v => "{{" + v + "}}")) + ".");
        }

        private static string UniqueKey(string key, HashSet<string> keys)
        {
            string k = key;
            int n = 2;
            while (!keys.Add(k)) k = key + "-" + n++;
            return k;
        }

        private static string StripComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') quoted = !quoted;
                if (line[i] == '#' && !quoted && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line.Substring(0, i).TrimEnd();
            }

            return line.TrimEnd();
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') || (value[0] == '\'' && value[value.Length - 1] == '\''))) return value.Substring(1, value.Length - 2);
            return value;
        }

        private static bool Bool(string value, out bool result)
        {
            if (String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "yes") { result = true; return true; }
            if (String.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "no") { result = false; return true; }
            result = false;
            return false;
        }

        private static void Error(BriefingTemplateDefinition def, int line, string message)
        {
            def.Problems.Add(new TemplateProblem { Line = line, Message = message });
        }

        private static void Warn(BriefingTemplateDefinition def, int line, string message)
        {
            def.Problems.Add(new TemplateProblem { Line = line, Message = message, IsWarning = true });
        }
    }
}
