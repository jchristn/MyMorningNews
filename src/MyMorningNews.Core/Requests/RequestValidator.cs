namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Checks request bodies against the <see cref="TextLimitAttribute"/> declarations on their properties.
    /// Thread safety: safe for concurrent use; property lookups are cached per type.
    /// </summary>
    public static class RequestValidator
    {
        private static readonly ConcurrentDictionary<Type, List<KeyValuePair<PropertyInfo, TextLimitAttribute>>> _Cache = new ConcurrentDictionary<Type, List<KeyValuePair<PropertyInfo, TextLimitAttribute>>>();

        /// <summary>
        /// Validate a request body. A list body is validated item by item.
        /// </summary>
        /// <param name="request">Request body; null has no errors.</param>
        /// <returns>Errors keyed by the JSON field name (camelCase, with an index for list bodies); empty when valid.</returns>
        public static Dictionary<string, string> Validate(object? request)
        {
            Dictionary<string, string> errors = new Dictionary<string, string>(StringComparer.Ordinal);
            if (request == null) return errors;
            if (request is IEnumerable items && !(request is string))
            {
                int index = 0;
                foreach (object? item in items)
                {
                    if (item != null) ValidateObject(item, "[" + index + "].", errors);
                    index++;
                }

                return errors;
            }

            ValidateObject(request, String.Empty, errors);
            return errors;
        }

        /// <summary>
        /// Describe a set of errors in one sentence for the error response message.
        /// </summary>
        /// <param name="errors">Errors from <see cref="Validate"/>.</param>
        /// <returns>The single message when there is one error, otherwise a summary listing them.</returns>
        /// <exception cref="ArgumentNullException">errors is null.</exception>
        public static string Summarize(Dictionary<string, string> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (errors.Count == 0) return "The request is valid.";
            if (errors.Count == 1) return errors.Values.First();
            return "The request is not valid: " + String.Join(" ", errors.Values);
        }

        private static void ValidateObject(object request, string prefix, Dictionary<string, string> errors)
        {
            foreach (KeyValuePair<PropertyInfo, TextLimitAttribute> entry in Limits(request.GetType()))
            {
                string field = prefix + JsonNamingPolicy.CamelCase.ConvertName(entry.Key.Name);
                object? value = entry.Key.GetValue(request);
                if (value is string text)
                {
                    string? error = Check(field, text, entry.Value);
                    if (error != null) errors[field] = error;
                }
                else if (value is IEnumerable<string?> list)
                {
                    int index = 0;
                    foreach (string? item in list)
                    {
                        string? error = item == null ? null : Check(field + "[" + index + "]", item, entry.Value);
                        if (error != null)
                        {
                            errors[field] = error;
                            break;
                        }

                        index++;
                    }
                }
            }
        }

        private static string? Check(string field, string value, TextLimitAttribute limit)
        {
            int length = value.Trim().Length;
            if (!limit.AllowBlank && length == 0) return field + " must be 1 to " + limit.Maximum + " characters and cannot be blank.";
            if (length > limit.Maximum) return field + " must be " + (limit.AllowBlank ? "at most " : "1 to ") + limit.Maximum + " characters.";
            return null;
        }

        private static List<KeyValuePair<PropertyInfo, TextLimitAttribute>> Limits(Type type)
        {
            return _Cache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p => new KeyValuePair<PropertyInfo, TextLimitAttribute?>(p, p.GetCustomAttribute<TextLimitAttribute>(true)))
                .Where(kv => kv.Value != null)
                .Select(kv => new KeyValuePair<PropertyInfo, TextLimitAttribute>(kv.Key, kv.Value!))
                .ToList());
        }
    }
}
