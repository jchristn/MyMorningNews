namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON settings shared by the client, datasets, and reports: camelCase, enums as strings, case-insensitive reads.
    /// Files are written as UTF-8 without a byte order mark.
    /// </summary>
    public static class Json
    {
        #region Public-Members

        /// <summary>
        /// Options for reading and writing.
        /// </summary>
        public static readonly JsonSerializerOptions Options = Create(false);

        /// <summary>
        /// Options for indented output.
        /// </summary>
        public static readonly JsonSerializerOptions Indented = Create(true);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize a value.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="indented">Indent the output.</param>
        /// <returns>JSON.</returns>
        public static string Serialize(object? value, bool indented = false)
        {
            return JsonSerializer.Serialize(value, indented ? Indented : Options);
        }

        /// <summary>
        /// Deserialize a value.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON.</param>
        /// <returns>Value.</returns>
        /// <exception cref="JsonException">The JSON is invalid or empty.</exception>
        public static T Deserialize<T>(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Expected a " + typeof(T).Name + " but the JSON was null.");
        }

        /// <summary>
        /// Write a value to a file as indented UTF-8 without a byte order mark, creating the directory.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="value">Value.</param>
        public static void WriteFile(string path, object? value)
        {
            ArgumentNullException.ThrowIfNull(path);
            string? dir = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, Serialize(value, true) + "\n", new UTF8Encoding(false));
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions Create(bool indented)
        {
            JsonSerializerOptions o = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            o.Converters.Add(new JsonStringEnumConverter());
            return o;
        }

        #endregion
    }
}
