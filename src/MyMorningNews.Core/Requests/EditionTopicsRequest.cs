namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Set an edition topics in order (empty means all active recurring topics).
    /// </summary>
    public class EditionTopicsRequest
    {
        /// <summary>
        /// Topic IDs.
        /// </summary>
        public List<string> TopicIds { get; set; } = new List<string>();
    }
}
