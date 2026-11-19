namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Accept suggested sources as topic constraints.
    /// </summary>
    public class SourceAcceptRequest
    {
        /// <summary>
        /// Feed URLs to add.
        /// </summary>
        public List<string> FeedUrls { get; set; } = new List<string>();

        /// <summary>
        /// Seed page URLs to add.
        /// </summary>
        public List<string> SeedUrls { get; set; } = new List<string>();
    }
}
