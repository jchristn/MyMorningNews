namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Base class for resource groups: holds the client and builds query strings.
    /// Thread safety: stateless apart from the client reference; safe to share.
    /// </summary>
    public abstract class ResourceBase
    {
        /// <summary>
        /// The client this resource group sends requests through.
        /// </summary>
        protected MyMorningNewsClient Client { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        protected ResourceBase(MyMorningNewsClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            Client = client;
        }

        /// <summary>
        /// Build query values from alternating keys and values. Null values are skipped.
        /// </summary>
        /// <param name="keysAndValues">Key, value, key, value, and so on.</param>
        /// <returns>Query values.</returns>
        /// <exception cref="ArgumentException">An odd number of arguments, or a key that is not a string.</exception>
        protected static Dictionary<string, string> Query(params object?[] keysAndValues)
        {
            ArgumentNullException.ThrowIfNull(keysAndValues);
            if (keysAndValues.Length % 2 != 0) throw new ArgumentException("Pass keys and values in pairs.", nameof(keysAndValues));
            Dictionary<string, string> query = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < keysAndValues.Length; i += 2)
            {
                if (keysAndValues[i] is not string key) throw new ArgumentException("Query keys must be strings.", nameof(keysAndValues));
                QueryFormat.Add(query, key, keysAndValues[i + 1]);
            }

            return query;
        }
    }
}
