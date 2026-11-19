namespace MyMorningNews.Core.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Central JSON serializer. Camel-case property names, string enums, nulls omitted.
    /// Thread safety: stateless after construction; safe to share.
    /// </summary>
    public static class Serializer
    {
        #region Public-Members

        /// <summary>
        /// Shared serializer options.
        /// </summary>
        public static JsonSerializerOptions Options { get; } = CreateOptions(false);

        /// <summary>
        /// Shared serializer options with indentation, used for settings files.
        /// </summary>
        public static JsonSerializerOptions IndentedOptions { get; } = CreateOptions(true);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="value">Object to serialize; null serializes to "null".</param>
        /// <param name="indented">Whether to indent the output.</param>
        /// <returns>JSON string.</returns>
        public static string Serialize(object? value, bool indented = false)
        {
            if (value == null) return "null";
            return JsonSerializer.Serialize(value, value.GetType(), indented ? IndentedOptions : Options);
        }

        /// <summary>
        /// Deserialize JSON into a typed object.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON text.</param>
        /// <returns>Deserialized object, or null when the JSON is the literal null.</returns>
        /// <exception cref="ArgumentNullException">json is null or empty.</exception>
        /// <exception cref="JsonException">json is malformed or does not match the type.</exception>
        public static T? Deserialize<T>(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) throw new ArgumentNullException(nameof(json));
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions CreateOptions(bool indented)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = indented
            };
            options.Converters.Add(new JsonStringEnumConverter(null, false));
            return options;
        }

        #endregion
    }
}
