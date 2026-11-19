namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Default briefing templates shipped as embedded Markdown (Defaults/Templates/*.md), keyed by file name.
    /// </summary>
    public static class DefaultTemplateCatalog
    {
        /// <summary>
        /// Key of the edition default.
        /// </summary>
        public const string MorningNews = "morning-news";

        /// <summary>
        /// Key of the single-run default.
        /// </summary>
        public const string RunBriefing = "run-briefing";

        /// <summary>
        /// Key of the one-time report default.
        /// </summary>
        public const string OneTimeReport = "one-time-report";

        private static readonly Lazy<Dictionary<string, string>> _All = new Lazy<Dictionary<string, string>>(Load);

        /// <summary>
        /// All defaults: key to content.
        /// </summary>
        /// <returns>Defaults.</returns>
        public static IReadOnlyDictionary<string, string> All()
        {
            return _All.Value;
        }

        /// <summary>
        /// One default's content.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Content.</returns>
        /// <exception cref="KeyNotFoundException">Unknown key.</exception>
        public static string Get(string key)
        {
            return _All.Value.TryGetValue(key ?? String.Empty, out string? c) ? c : throw new KeyNotFoundException("No default template '" + key + "'.");
        }

        private static Dictionary<string, string> Load()
        {
            Assembly assembly = typeof(DefaultTemplateCatalog).Assembly;
            Dictionary<string, string> all = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Defaults.Templates.", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal)))
            {
                string key = name.Substring(name.IndexOf(".Defaults.Templates.", StringComparison.Ordinal) + ".Defaults.Templates.".Length);
                key = key.Substring(0, key.Length - 3);
                using (Stream s = assembly.GetManifestResourceStream(name)!)
                using (StreamReader r = new StreamReader(s))
                {
                    all[key] = r.ReadToEnd().Replace("\r\n", "\n");
                }
            }

            return all;
        }
    }
}
