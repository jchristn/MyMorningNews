namespace MyMorningNews.Core.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// The catalog publications an edition includes, in order. Each must be one the caller is subscribed to. At most 20.
    /// </summary>
    public class EditionPublicationsRequest
    {
        /// <summary>
        /// Publication IDs (prefix cat_) in order. Empty removes every publication. Duplicates are rejected.
        /// </summary>
        public List<string> PublicationIds { get; set; } = new List<string>();
    }
}
