namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Tags for one feedback reason.
    /// </summary>
    public class TaggedReason
    {
        /// <summary>
        /// Feedback ID.
        /// </summary>
        [Documentation("The feedback id given with the item.")]
        public string FeedbackId { get; set; } = String.Empty;

        /// <summary>
        /// Tags.
        /// </summary>
        [Documentation("One to three short lowercase tags of 2 to 4 words.")]
        public List<string> Tags { get; set; } = new List<string>();
    }
}
