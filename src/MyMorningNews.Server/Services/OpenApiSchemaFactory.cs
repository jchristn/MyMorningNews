namespace MyMorningNews.Server.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using MyMorningNews.Core.Helpers;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Builds OpenAPI schemas from request and response types (camelCase names, string enums) and keeps the named
    /// component schemas the document references.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public static class OpenApiSchemaFactory
    {
        #region Public-Members

        /// <summary>
        /// Named component schemas, keyed by type name. Assigned to the OpenAPI settings at startup.
        /// </summary>
        public static Dictionary<string, OpenApiSchemaMetadata> Components { get; } = new Dictionary<string, OpenApiSchemaMetadata>(StringComparer.Ordinal);

        #endregion

        #region Private-Members

        private static readonly object _Lock = new object();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Schema for a type: inline for primitives, enums, and collections; a component reference for objects.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Schema.</returns>
        /// <exception cref="ArgumentNullException">type is null.</exception>
        public static OpenApiSchemaMetadata For(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            lock (_Lock)
            {
                return Build(type, 0);
            }
        }

        #endregion

        #region Private-Methods

        private static OpenApiSchemaMetadata Build(Type type, int depth)
        {
            Type t = Nullable.GetUnderlyingType(type) ?? type;
            if (t == typeof(string)) return OpenApiSchemaMetadata.String();
            if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return OpenApiSchemaMetadata.String("date-time");
            if (t == typeof(bool)) return OpenApiSchemaMetadata.Boolean();
            if (t == typeof(int) || t == typeof(short)) return OpenApiSchemaMetadata.Integer();
            if (t == typeof(long)) return OpenApiSchemaMetadata.Integer("int64");
            if (t == typeof(double) || t == typeof(float)) return OpenApiSchemaMetadata.Number("double");
            if (t == typeof(decimal)) return OpenApiSchemaMetadata.Number();
            if (t == typeof(object)) return new OpenApiSchemaMetadata();
            if (t.IsEnum)
            {
                OpenApiSchemaMetadata e = OpenApiSchemaMetadata.String();
                e.Enum = Enum.GetNames(t).Cast<object>().ToList();
                return e;
            }

            if (typeof(IDictionary).IsAssignableFrom(t)) return OpenApiSchemaMetadata.Create("object");
            if (typeof(IEnumerable).IsAssignableFrom(t))
            {
                Type item = t.IsArray ? t.GetElementType()! : (t.GetGenericArguments().FirstOrDefault() ?? typeof(object));
                return OpenApiSchemaMetadata.CreateArray(Build(item, depth + 1));
            }

            string name = ComponentName(t);
            if (!Components.ContainsKey(name))
            {
                OpenApiSchemaMetadata obj = OpenApiSchemaMetadata.Create("object");
                obj.Properties = new Dictionary<string, OpenApiSchemaMetadata>(StringComparer.Ordinal);
                Components[name] = obj;
                if (depth < 8)
                {
                    foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                    {
                        string prop = Char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                        OpenApiSchemaMetadata s = Build(p.PropertyType, depth + 1);
                        DocumentationAttribute? doc = p.GetCustomAttribute<DocumentationAttribute>();
                        if (doc != null && s.Ref == null) s.Description = doc.Description;
                        obj.Properties[prop] = s;
                    }
                }
            }

            return OpenApiSchemaMetadata.CreateRef(name);
        }

        private static string ComponentName(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            string root = t.Name.Substring(0, t.Name.IndexOf('`'));
            return root + "Of" + String.Join("And", t.GetGenericArguments().Select(ComponentName));
        }

        #endregion
    }
}
