namespace MyMorningNews.Core.Serialization
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Turns a <see cref="JsonException"/> from reading a request body into a message for people: the field that was wrong
    /// and the kind of value it takes (with the valid values for an enum), never a .NET type name.
    /// Thread safety: stateless; safe for concurrent use.
    /// </summary>
    public static class JsonErrorDescriber
    {
        /// <summary>
        /// Describe a JSON error.
        /// </summary>
        /// <param name="ex">Exception thrown while deserializing.</param>
        /// <param name="target">Type the body was read into, used to find the field's type; null describes the position only.</param>
        /// <returns>Message.</returns>
        /// <exception cref="ArgumentNullException">ex is null.</exception>
        public static string Describe(JsonException ex, Type? target)
        {
            ArgumentNullException.ThrowIfNull(ex);
            // A value of the wrong type ("could not be converted") names the field; a syntax error gives the position.
            bool conversion = ex.Message.Contains("could not be converted", StringComparison.Ordinal);
            string? field = conversion ? FieldName(ex.Path) : null;
            Type? fieldType = field != null && target != null ? ResolvePath(target, ex.Path!) : null;
            if (field != null && fieldType != null) return field + " must be " + Expectation(fieldType) + ".";
            if (field != null) return "The value of " + field + " is not valid.";
            string where = ex.LineNumber.HasValue ? " (line " + (ex.LineNumber.Value + 1) + ", position " + ((ex.BytePositionInLine ?? 0) + 1) + ")" : String.Empty;
            return "The request body is not valid JSON" + where + ".";
        }

        /// <summary>
        /// Describe in words the kind of JSON value a type takes.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Description, such as "a whole number" or "one of: Daily, Weekly".</returns>
        /// <exception cref="ArgumentNullException">type is null.</exception>
        public static string Expectation(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            Type t = Nullable.GetUnderlyingType(type) ?? type;
            if (t.IsEnum) return "one of: " + String.Join(", ", Enum.GetNames(t));
            if (t == typeof(string)) return "text";
            if (t == typeof(bool)) return "true or false";
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return "a whole number";
            if (t == typeof(decimal) || t == typeof(double) || t == typeof(float)) return "a number";
            if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "a date and time in ISO 8601 form, such as 2026-01-31T08:00:00Z";
            if (t == typeof(TimeSpan)) return "a duration such as 00:30:00";
            if (t == typeof(Guid)) return "an identifier";
            if (t.IsArray || (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType && !IsDictionary(t))) return "a list";
            return "an object";
        }

        private static bool IsDictionary(Type t)
        {
            return t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static string? FieldName(string? path)
        {
            if (String.IsNullOrEmpty(path) || path == "$") return null;
            string p = path.StartsWith("$.", StringComparison.Ordinal) ? path.Substring(2) : (path.StartsWith("$", StringComparison.Ordinal) ? path.Substring(1) : path);
            return p.Length == 0 ? null : p;
        }

        private static Type? ResolvePath(Type root, string path)
        {
            Type? current = root;
            string p = path.StartsWith("$", StringComparison.Ordinal) ? path.Substring(1) : path;
            int i = 0;
            while (current != null && i < p.Length)
            {
                if (p[i] == '.')
                {
                    int end = i + 1;
                    while (end < p.Length && p[end] != '.' && p[end] != '[') end++;
                    string name = p.Substring(i + 1, end - i - 1);
                    PropertyInfo? prop = current.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                    current = prop?.PropertyType;
                    i = end;
                }
                else if (p[i] == '[')
                {
                    int end = p.IndexOf(']', i);
                    if (end < 0) return null;
                    current = ElementType(current);
                    i = end + 1;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        private static Type? ElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType)
            {
                Type[] args = t.GetGenericArguments();
                return args.Length == 1 ? args[0] : (args.Length == 2 ? args[1] : null);
            }

            return null;
        }
    }
}
