namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Small builders for JSON Schema fragments used in tool input and output schemas. Thread safe (stateless).
    /// </summary>
    public static class Schema
    {
        /// <summary>
        /// An object schema.
        /// </summary>
        /// <param name="properties">Property schemas by name.</param>
        /// <param name="required">Required property names, or null.</param>
        /// <param name="additionalProperties">Whether other properties are allowed. Default false.</param>
        /// <returns>Schema.</returns>
        /// <exception cref="ArgumentNullException">Properties are null.</exception>
        public static Dictionary<string, object> Object(Dictionary<string, object> properties, IEnumerable<string>? required = null, bool additionalProperties = false)
        {
            ArgumentNullException.ThrowIfNull(properties);
            Dictionary<string, object> s = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "type", "object" },
                { "properties", properties },
                { "additionalProperties", additionalProperties }
            };
            List<string> req = required?.ToList() ?? new List<string>();
            if (req.Any()) s["required"] = req;
            return s;
        }

        /// <summary>
        /// A string schema.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="maxLength">Maximum length, or 0 for none.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> String(string description, int maxLength = 0)
        {
            Dictionary<string, object> s = new Dictionary<string, object>(StringComparer.Ordinal) { { "type", "string" }, { "description", description } };
            if (maxLength > 0) s["maxLength"] = maxLength;
            return s;
        }

        /// <summary>
        /// A string that may be null.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> NullableString(string description)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "type", new[] { "string", "null" } }, { "description", description } };
        }

        /// <summary>
        /// A string limited to listed values.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="values">Allowed values.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Choice(string description, params string[] values)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "type", "string" }, { "description", description }, { "enum", values } };
        }

        /// <summary>
        /// A string limited to the names of an enum.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="description">Description.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Choice<T>(string description) where T : struct, Enum
        {
            return Choice(description, Enum.GetNames<T>());
        }

        /// <summary>
        /// An integer schema.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="minimum">Minimum.</param>
        /// <param name="maximum">Maximum.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Integer(string description, long minimum, long maximum)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "type", "integer" }, { "description", description }, { "minimum", minimum }, { "maximum", maximum } };
        }

        /// <summary>
        /// A number schema, optionally nullable.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="nullable">Whether null is allowed.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Number(string description, bool nullable = false)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "type", nullable ? new[] { "number", "null" } : (object)"number" }, { "description", description } };
        }

        /// <summary>
        /// A boolean schema.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Boolean(string description)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "type", "boolean" }, { "description", description } };
        }

        /// <summary>
        /// An array schema.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="items">Item schema.</param>
        /// <param name="maxItems">Maximum items, or 0 for none.</param>
        /// <returns>Schema.</returns>
        /// <exception cref="ArgumentNullException">Items are null.</exception>
        public static Dictionary<string, object> Array(string description, object items, int maxItems = 0)
        {
            ArgumentNullException.ThrowIfNull(items);
            Dictionary<string, object> s = new Dictionary<string, object>(StringComparer.Ordinal) { { "type", "array" }, { "description", description }, { "items", items } };
            if (maxItems > 0) s["maxItems"] = maxItems;
            return s;
        }

        /// <summary>
        /// A loose object schema for nested records whose exact shape is documented in MCP_API.md.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="nullable">Whether null is allowed.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Record(string description, bool nullable = false)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { "type", nullable ? new[] { "object", "null" } : (object)"object" }, { "description", description }, { "additionalProperties", true } };
        }

        /// <summary>
        /// Build a property map from name and schema pairs given as alternating arguments.
        /// </summary>
        /// <param name="pairs">Name, schema, name, schema, and so on.</param>
        /// <returns>Property map.</returns>
        /// <exception cref="ArgumentException">Odd number of arguments or a name that is not a string.</exception>
        public static Dictionary<string, object> Props(params object[] pairs)
        {
            ArgumentNullException.ThrowIfNull(pairs);
            if (pairs.Length % 2 != 0) throw new ArgumentException("Props takes name and schema pairs.", nameof(pairs));
            Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                if (pairs[i] is not string name) throw new ArgumentException("Property names must be strings.", nameof(pairs));
                map[name] = pairs[i + 1];
            }

            return map;
        }

        /// <summary>
        /// The output schema of a paged list: items of the given shape plus totals.
        /// </summary>
        /// <param name="item">Item schema.</param>
        /// <returns>Schema.</returns>
        public static Dictionary<string, object> Page(object item)
        {
            return Object(Props(
                "items", Array("Items on this page.", item),
                "totalCount", Integer("Items matching across all pages.", 0, long.MaxValue),
                "pageNumber", Integer("Page number (1-based).", 1, int.MaxValue),
                "totalPages", Integer("Pages available.", 0, int.MaxValue)), new[] { "items", "totalCount" });
        }
    }
}
