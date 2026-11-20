namespace MyMorningNews.Sdk.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Creates a <see cref="TolerantEnumConverter{T}"/> for every enum type. Nullable enums are handled by the serializer,
    /// which wraps the converter for the underlying type.
    /// Thread safety: stateless; safe to share.
    /// </summary>
    public class TolerantEnumConverterFactory : JsonConverterFactory
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public TolerantEnumConverterFactory()
        {
        }

        /// <summary>
        /// Whether the type is an enum.
        /// </summary>
        /// <param name="typeToConvert">Type.</param>
        /// <returns>True for enum types.</returns>
        public override bool CanConvert(Type typeToConvert)
        {
            ArgumentNullException.ThrowIfNull(typeToConvert);
            return typeToConvert.IsEnum;
        }

        /// <summary>
        /// Create a converter for an enum type.
        /// </summary>
        /// <param name="typeToConvert">Enum type.</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>Converter.</returns>
        /// <exception cref="InvalidOperationException">The converter could not be created.</exception>
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(typeToConvert);
            Type converterType = typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert);
            object? created = Activator.CreateInstance(converterType);
            if (created is JsonConverter converter) return converter;
            throw new InvalidOperationException("Could not create an enum converter for " + typeToConvert.Name + ".");
        }
    }
}
