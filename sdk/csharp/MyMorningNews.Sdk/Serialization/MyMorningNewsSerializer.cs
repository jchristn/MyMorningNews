namespace MyMorningNews.Sdk.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON settings matching the server: camelCase names, enums as strings (unknown names tolerated), nulls omitted when
    /// writing, and case-insensitive property names when reading.
    /// Thread safety: the options are immutable after first use; safe to share.
    /// </summary>
    public static class MyMorningNewsSerializer
    {
        /// <summary>
        /// Shared serializer options.
        /// </summary>
        public static JsonSerializerOptions Options { get; } = CreateOptions();

        /// <summary>
        /// Serialize a value to JSON.
        /// </summary>
        /// <param name="value">Value; null serializes to "null".</param>
        /// <returns>JSON text.</returns>
        public static string Serialize(object? value)
        {
            if (value == null) return "null";
            return JsonSerializer.Serialize(value, value.GetType(), Options);
        }

        /// <summary>
        /// Deserialize JSON into a typed value.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON text.</param>
        /// <returns>Value, or null for the literal null.</returns>
        /// <exception cref="ArgumentNullException">json is null or empty.</exception>
        /// <exception cref="JsonException">json is malformed or does not match the type.</exception>
        public static T? Deserialize<T>(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) throw new ArgumentNullException(nameof(json));
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            options.Converters.Add(new TolerantEnumConverterFactory());
            return options;
        }
    }
}
