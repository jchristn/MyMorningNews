namespace MyMorningNews.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Command-line arguments: a command word, then --name value pairs, with a bare --flag meaning true. Every value a
    /// command reads (including defaults) is recorded so the report can reproduce the run. Secret options are recorded
    /// as "(set)" only. Not thread safe.
    /// </summary>
    public class BenchmarkArguments
    {
        #region Public-Members

        /// <summary>
        /// The command word, lowercase. Empty when none was given.
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// Every option value a command read, including defaults, in the order read. Secrets are masked.
        /// </summary>
        public SortedDictionary<string, string> Used { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The original arguments, with secret values masked.
        /// </summary>
        public string CommandLine { get; }

        #endregion

        #region Private-Members

        private static readonly HashSet<string> _Secrets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin-key", "api-key", "password" };
        private readonly Dictionary<string, string> _Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Parse arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <exception cref="ArgumentNullException">args is null.</exception>
        /// <exception cref="ArgumentException">An argument is not an option.</exception>
        public BenchmarkArguments(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Command = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0].Trim().ToLowerInvariant() : String.Empty;
            List<string> shown = new List<string>();
            if (Command.Length > 0) shown.Add(Command);
            for (int i = Command.Length > 0 ? 1 : 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith("--", StringComparison.Ordinal) || a.Length < 3) throw new ArgumentException("Unexpected argument '" + a + "'. Options look like --name value.");
                string name = a.Substring(2);
                string value = "true";
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[i + 1];
                    i++;
                }

                _Values[name] = value;
                shown.Add("--" + name);
                if (value != "true") shown.Add(_Secrets.Contains(name) ? "(set)" : Quote(value));
            }

            CommandLine = "MyMorningNews.Benchmark " + String.Join(" ", shown);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Whether an option was given.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <returns>True when present.</returns>
        public bool Has(string name)
        {
            return _Values.ContainsKey(name);
        }

        /// <summary>
        /// A string option.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <param name="defaultValue">Default when absent.</param>
        /// <returns>Value.</returns>
        public string GetString(string name, string defaultValue)
        {
            string value = _Values.TryGetValue(name, out string? v) ? v : defaultValue;
            Used[name] = _Secrets.Contains(name) ? (String.IsNullOrEmpty(value) ? String.Empty : "(set)") : value;
            return value;
        }

        /// <summary>
        /// An optional string option.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <returns>Value, or null when absent.</returns>
        public string? GetOptional(string name)
        {
            string? value = _Values.TryGetValue(name, out string? v) ? v : null;
            Used[name] = value == null ? "(none)" : _Secrets.Contains(name) ? "(set)" : value;
            return value;
        }

        /// <summary>
        /// An integer option.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <param name="defaultValue">Default when absent.</param>
        /// <param name="minimum">Minimum allowed value.</param>
        /// <param name="maximum">Maximum allowed value.</param>
        /// <returns>Value, clamped to the range.</returns>
        /// <exception cref="FormatException">The value is not an integer.</exception>
        public int GetInt(string name, int defaultValue, int minimum, int maximum)
        {
            int value = defaultValue;
            if (_Values.TryGetValue(name, out string? v) && !Int32.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new FormatException("--" + name + " expects an integer, got '" + v + "'.");
            value = Math.Clamp(value, minimum, maximum);
            Used[name] = value.ToString(CultureInfo.InvariantCulture);
            return value;
        }

        /// <summary>
        /// A floating-point option.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <param name="defaultValue">Default when absent.</param>
        /// <param name="minimum">Minimum allowed value.</param>
        /// <param name="maximum">Maximum allowed value.</param>
        /// <returns>Value, clamped to the range.</returns>
        /// <exception cref="FormatException">The value is not a number.</exception>
        public double GetDouble(string name, double defaultValue, double minimum, double maximum)
        {
            double value = defaultValue;
            if (_Values.TryGetValue(name, out string? v) && !Double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("--" + name + " expects a number, got '" + v + "'.");
            value = Math.Clamp(value, minimum, maximum);
            Used[name] = value.ToString("0.####", CultureInfo.InvariantCulture);
            return value;
        }

        /// <summary>
        /// A boolean flag: present (or "true") means true.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <returns>Value.</returns>
        public bool GetFlag(string name)
        {
            bool value = _Values.TryGetValue(name, out string? v) && !String.Equals(v, "false", StringComparison.OrdinalIgnoreCase);
            Used[name] = value ? "true" : "false";
            return value;
        }

        /// <summary>
        /// A comma-separated list option.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <param name="defaultValue">Default when absent, comma separated.</param>
        /// <returns>Trimmed, non-empty values.</returns>
        public List<string> GetList(string name, string defaultValue)
        {
            string raw = _Values.TryGetValue(name, out string? v) ? v : defaultValue;
            Used[name] = raw;
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        /// <summary>
        /// A comma-separated list of integers.
        /// </summary>
        /// <param name="name">Option name without dashes.</param>
        /// <param name="defaultValue">Default when absent, comma separated.</param>
        /// <returns>Values.</returns>
        /// <exception cref="FormatException">A value is not an integer.</exception>
        public List<int> GetIntList(string name, string defaultValue)
        {
            List<int> values = new List<int>();
            foreach (string s in GetList(name, defaultValue))
            {
                if (!Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) throw new FormatException("--" + name + " expects integers, got '" + s + "'.");
                values.Add(n);
            }

            return values;
        }

        #endregion

        #region Private-Methods

        private static string Quote(string value)
        {
            return value.Contains(' ', StringComparison.Ordinal) ? "\"" + value + "\"" : value;
        }

        #endregion
    }
}
