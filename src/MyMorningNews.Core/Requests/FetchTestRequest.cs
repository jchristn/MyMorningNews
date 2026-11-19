namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Fetch one page as the research pipeline would.
    /// </summary>
    public class FetchTestRequest
    {
        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;
    }
}
