namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>Administrator catalog metadata. A new publication creates an isolated canonical topic.</summary>
    public class CatalogPublicationRequest
    {
        /// <summary>Required title, 1 to 200 characters.</summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>Public research scope, up to 4000 characters.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Category, 1 to 100 characters.</summary>
        public string Category { get; set; } = "General";

        /// <summary>Output language, 1 to 16 characters.</summary>
        public string Language { get; set; } = "en";

        /// <summary>Allow free users to select this publication.</summary>
        public bool FreeEligible { get; set; } = false;

        /// <summary>Draft, Published, Retired, or Retracted.</summary>
        public string Status { get; set; } = "Draft";

        /// <summary>Display ordering.</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>Required current revision when updating; conflicts return 409.</summary>
        public long? Revision { get; set; } = null;

    }
}
