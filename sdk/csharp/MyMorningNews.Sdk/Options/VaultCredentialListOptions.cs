namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing vault credentials.
    /// </summary>
    public class VaultCredentialListOptions : ListOptions
    {
        /// <summary>
        /// Only active (true) or inactive (false) credentials. Null by default.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Only credentials of this kind. Null by default.
        /// </summary>
        public VaultCredentialKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Only credentials for this provider. Null by default.
        /// </summary>
        public VaultProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VaultCredentialListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "active", Active);
            QueryFormat.Add(query, "kind", Kind);
            QueryFormat.Add(query, "provider", Provider);
        }
    }
}
