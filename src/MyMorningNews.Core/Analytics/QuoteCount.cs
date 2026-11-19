namespace MyMorningNews.Core.Analytics
{
    using System;

    /// <summary>
    /// Quotes extracted and verified in one run.
    /// </summary>
    public class QuoteCount
    {
        /// <summary>
        /// Extraction model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Day of the run.
        /// </summary>
        public string Day { get; set; } = String.Empty;

        /// <summary>
        /// Quotes.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Verified quotes.
        /// </summary>
        public int Verified { get; set; } = 0;
    }
}
