namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// A record that references a vault credential.
    /// </summary>
    public class VaultUsageItem
    {
        /// <summary>
        /// Referencing resource type.
        /// </summary>
        public string ResourceType { get; set; } = String.Empty;

        /// <summary>
        /// Referencing resource identifier.
        /// </summary>
        public string ResourceId { get; set; } = String.Empty;

        /// <summary>
        /// Referencing resource name.
        /// </summary>
        public string Name { get; set; } = String.Empty;
    }
}
