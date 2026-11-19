namespace MyMorningNews.Core.Prompts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Parses prompt Markdown: a front matter block delimited by --- lines (key: value pairs and [a, b] lists),
    /// followed by "## System" and "## User" sections. Text before the first section counts as the user section.
    /// Thread safety: stateless.
    /// </summary>
    public static class PromptParser
    {
        /// <summary>
        /// Parse content.
        /// </summary>
        /// <param name="content">Markdown content.</param>
        /// <returns>Definition.</returns>
        /// <exception cref="ArgumentNullException">content is null or empty.</exception>
        /// <exception cref="FormatException">The front matter is malformed or the role is unknown.</exception>
        public static PromptDefinition Parse(string content)
        {
            if (String.IsNullOrWhiteSpace(content)) throw new ArgumentNullException(nameof(content));
            string text = content.Replace("\r\n", "\n", StringComparison.Ordinal);
            PromptDefinition def = new PromptDefinition { Content = content };
            string body = text;

            if (text.StartsWith("---\n", StringComparison.Ordinal))
            {
                int end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
                if (end < 0) throw new FormatException("The front matter is not closed with a --- line.");
                string front = text.Substring(4, end - 4);
                int bodyStart = text.IndexOf('\n', end + 4);
                body = bodyStart < 0 ? String.Empty : text.Substring(bodyStart + 1);
                foreach (string raw in front.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int colon = line.IndexOf(':');
                    if (colon <= 0) throw new FormatException("Front matter line '" + line + "' is not key: value.");
                    string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                    string value = line.Substring(colon + 1).Trim();
                    switch (key)
                    {
                        case "key": def.Key = value; break;
                        case "name": def.Name = value; break;
                        case "output": def.Output = value; break;
                        case "role":
                            if (!Enum.TryParse(value, true, out ModelTaskEnum role)) throw new FormatException("Unknown role '" + value + "'. Use one of: " + String.Join(", ", Enum.GetNames<ModelTaskEnum>()) + ".");
                            def.Role = role;
                            break;
                        case "variables":
                            def.Variables = value.Trim('[', ']').Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();
                            break;
                    }
                }
            }

            string current = "user";
            List<string> system = new List<string>();
            List<string> user = new List<string>();
            foreach (string line in body.Split('\n'))
            {
                string t = line.Trim();
                if (String.Equals(t, "## System", StringComparison.OrdinalIgnoreCase)) { current = "system"; continue; }
                if (String.Equals(t, "## User", StringComparison.OrdinalIgnoreCase)) { current = "user"; continue; }
                (current == "system" ? system : user).Add(line);
            }

            def.System = String.Join("\n", system).Trim();
            def.User = String.Join("\n", user).Trim();
            return def;
        }
    }
}
