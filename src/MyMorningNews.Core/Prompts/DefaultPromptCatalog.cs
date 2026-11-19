namespace MyMorningNews.Core.Prompts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Default prompts shipped as embedded Markdown resources (Defaults/Prompts/*.md).
    /// Thread safety: loaded once; read-only afterwards.
    /// </summary>
    public static class DefaultPromptCatalog
    {
        #region Private-Members

        private static readonly Lazy<Dictionary<string, PromptDefinition>> _Defaults = new Lazy<Dictionary<string, PromptDefinition>>(Load);

        #endregion

        #region Public-Methods

        /// <summary>
        /// All default prompts keyed by prompt key.
        /// </summary>
        /// <returns>Definitions.</returns>
        public static IReadOnlyDictionary<string, PromptDefinition> All()
        {
            return _Defaults.Value;
        }

        /// <summary>
        /// One default prompt.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Definition, or null.</returns>
        public static PromptDefinition? Get(string key)
        {
            return _Defaults.Value.TryGetValue(key ?? String.Empty, out PromptDefinition? def) ? def : null;
        }

        #endregion

        #region Private-Methods

        private static Dictionary<string, PromptDefinition> Load()
        {
            Assembly assembly = typeof(DefaultPromptCatalog).Assembly;
            Dictionary<string, PromptDefinition> result = new Dictionary<string, PromptDefinition>(StringComparer.Ordinal);
            foreach (string name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Defaults.Prompts.", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal)))
            {
                using (Stream? stream = assembly.GetManifestResourceStream(name))
                {
                    if (stream == null) continue;
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        PromptDefinition def = PromptParser.Parse(reader.ReadToEnd());
                        if (!String.IsNullOrEmpty(def.Key)) result[def.Key] = def;
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
