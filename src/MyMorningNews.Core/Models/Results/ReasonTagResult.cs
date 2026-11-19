namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of feedback.tag-reasons.
    /// </summary>
    public class ReasonTagResult : IStructuredResult
    {
        /// <summary>
        /// Tagged reasons.
        /// </summary>
        [Documentation("One entry per feedback item that has a reason.")]
        public List<TaggedReason> Items { get; set; } = new List<TaggedReason>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            if (Items == null) Items = new List<TaggedReason>();
            foreach (TaggedReason t in Items.Where(t => t != null))
                t.Tags = (t.Tags ?? new List<string>()).Select(x => (x ?? String.Empty).Trim().ToLowerInvariant()).Where(x => x.Length > 1 && x.Length <= 64).Distinct().Take(3).ToList();
            Items = Items.Where(t => t != null && !String.IsNullOrWhiteSpace(t.FeedbackId) && t.Tags.Count > 0).ToList();
            return new List<string>();
        }
    }
}
