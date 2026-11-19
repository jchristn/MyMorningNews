namespace MyMorningNews.Core.Prompts
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Logic-less renderer: {{name}} substitutes a value (missing renders as "(none)"), and
    /// {{#each list}}...{{/each}} repeats a block, where {{this}} and {{this.property}} read the current item.
    /// Thread safety: stateless.
    /// </summary>
    public static class PromptRenderer
    {
        #region Public-Members

        /// <summary>
        /// Text rendered for missing or empty values.
        /// </summary>
        public const string EmptyValue = "(none)";

        #endregion

        #region Private-Members

        private static readonly Regex _Each = new Regex(@"\{\{#each\s+([A-Za-z0-9_.]+)\s*\}\}(.*?)\{\{/each\}\}", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _Var = new Regex(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}", RegexOptions.Compiled);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Render a template.
        /// </summary>
        /// <param name="template">Template text.</param>
        /// <param name="values">Values keyed by variable name.</param>
        /// <returns>Rendered text.</returns>
        public static string Render(string template, IReadOnlyDictionary<string, object?> values)
        {
            if (String.IsNullOrEmpty(template)) return String.Empty;
            ArgumentNullException.ThrowIfNull(values);
            string expanded = _Each.Replace(template, m =>
            {
                values.TryGetValue(m.Groups[1].Value, out object? list);
                if (list is not IEnumerable items || list is string) return String.Empty;
                StringBuilder sb = new StringBuilder();
                foreach (object? item in items)
                {
                    sb.Append(_Var.Replace(m.Groups[2].Value, v =>
                    {
                        string name = v.Groups[1].Value;
                        if (name == "this") return Format(item);
                        if (name.StartsWith("this.", StringComparison.Ordinal)) return Format(ReadProperty(item, name.Substring(5)));
                        return v.Value;
                    }));
                }

                return sb.ToString();
            });

            return _Var.Replace(expanded, m => values.TryGetValue(m.Groups[1].Value, out object? v) ? Format(v) : EmptyValue);
        }

        /// <summary>
        /// Variables referenced in a template.
        /// </summary>
        /// <param name="template">Template text.</param>
        /// <returns>Distinct variable names (excluding this and this.*).</returns>
        public static List<string> Referenced(string template)
        {
            if (String.IsNullOrEmpty(template)) return new List<string>();
            List<string> names = _Var.Matches(template).Select(m => m.Groups[1].Value).ToList();
            names.AddRange(_Each.Matches(template).Select(m => m.Groups[1].Value));
            return names.Where(n => n != "this" && !n.StartsWith("this.", StringComparison.Ordinal) && !n.StartsWith("#", StringComparison.Ordinal)).Distinct().ToList();
        }

        /// <summary>
        /// Variables referenced but not declared.
        /// </summary>
        /// <param name="def">Definition.</param>
        /// <returns>Unknown variables.</returns>
        public static List<string> Unknown(PromptDefinition def)
        {
            ArgumentNullException.ThrowIfNull(def);
            HashSet<string> declared = new HashSet<string>(def.Variables, StringComparer.Ordinal);
            return Referenced(def.System + "\n" + def.User).Where(n => !declared.Contains(n)).ToList();
        }

        #endregion

        #region Private-Methods

        private static string Format(object? value)
        {
            if (value == null) return EmptyValue;
            if (value is string s) return s.Trim().Length == 0 ? EmptyValue : s;
            if (value is DateTime d) return d.ToString("yyyy-MM-dd");
            if (value is IEnumerable e)
            {
                List<string> items = e.Cast<object?>().Select(x => "- " + Format(x)).ToList();
                return items.Count == 0 ? EmptyValue : String.Join("\n", items);
            }

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? EmptyValue;
        }

        private static object? ReadProperty(object? item, string name)
        {
            if (item == null) return null;
            if (item is IDictionary<string, object?> dict) return dict.TryGetValue(name, out object? v) ? v : null;
            PropertyInfo? p = item.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return p?.GetValue(item);
        }

        #endregion
    }
}
