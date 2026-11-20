namespace MyMorningNews.Sdk.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads enum values written as names (case-insensitive) or numbers, and writes them as names. A name this SDK does not
    /// know, such as a member added by a newer server, reads as the enum's default value (its first member) instead of failing.
    /// Thread safety: stateless; safe to share.
    /// </summary>
    /// <typeparam name="T">Enum type.</typeparam>
    public class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public TolerantEnumConverter()
        {
        }

        /// <summary>
        /// Read a value.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="typeToConvert">Type being converted.</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>The parsed member, or default(T) when the value is unknown.</returns>
        /// <exception cref="JsonException">The token is neither a string nor a number.</exception>
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? text = reader.GetString();
                if (!String.IsNullOrWhiteSpace(text) && Enum.TryParse<T>(text.Trim(), true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;
                return default;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out long number))
                {
                    T value = (T)Enum.ToObject(typeof(T), number);
                    return Enum.IsDefined(typeof(T), value) ? value : default;
                }

                return default;
            }

            throw new JsonException("Expected a string or number for " + typeof(T).Name + ".");
        }

        /// <summary>
        /// Write a value as its member name.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="value">Value.</param>
        /// <param name="options">Serializer options.</param>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value.ToString());
        }
    }
}
