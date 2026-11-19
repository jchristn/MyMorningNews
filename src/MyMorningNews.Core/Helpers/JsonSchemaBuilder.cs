namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Builds a JSON Schema (draft 2020-12 subset) for a result type: objects, strings, numbers, booleans, enums, lists,
    /// and nested objects, with camelCase property names. Non-nullable reference and value properties are required.
    /// Thread safety: stateless.
    /// </summary>
    public static class JsonSchemaBuilder
    {
        /// <summary>
        /// Build a schema for a type.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Schema as nested dictionaries.</returns>
        /// <exception cref="ArgumentNullException">type is null.</exception>
        public static Dictionary<string, object> Build(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return Schema(type, 0);
        }

        private static Dictionary<string, object> Schema(Type type, int depth)
        {
            Type t = Nullable.GetUnderlyingType(type) ?? type;
            if (t == typeof(string) || t == typeof(DateTime)) return new Dictionary<string, object> { { "type", "string" } };
            if (t == typeof(bool)) return new Dictionary<string, object> { { "type", "boolean" } };
            if (t == typeof(int) || t == typeof(long)) return new Dictionary<string, object> { { "type", "integer" } };
            if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return new Dictionary<string, object> { { "type", "number" } };
            if (t.IsEnum) return new Dictionary<string, object> { { "type", "string" }, { "enum", Enum.GetNames(t).ToList() } };
            if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t))
            {
                Type item = t.IsArray ? t.GetElementType()! : (t.GetGenericArguments().FirstOrDefault() ?? typeof(string));
                return new Dictionary<string, object> { { "type", "array" }, { "items", Schema(item, depth + 1) } };
            }

            if (depth > 6) return new Dictionary<string, object> { { "type", "object" } };
            Dictionary<string, object> props = new Dictionary<string, object>();
            List<string> required = new List<string>();
            NullabilityInfoContext nullability = new NullabilityInfoContext();
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
            {
                string name = Char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                Dictionary<string, object> s = Schema(p.PropertyType, depth + 1);
                DocumentationAttribute? doc = p.GetCustomAttribute<DocumentationAttribute>();
                if (doc != null) s["description"] = doc.Description;
                props[name] = s;
                bool nullable = Nullable.GetUnderlyingType(p.PropertyType) != null || nullability.Create(p).WriteState == NullabilityState.Nullable;
                if (!nullable) required.Add(name);
            }

            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", props },
                { "required", required },
                { "additionalProperties", false }
            };
        }
    }
}
