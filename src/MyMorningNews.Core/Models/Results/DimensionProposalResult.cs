namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of refinement.dimensions.
    /// </summary>
    public class DimensionProposalResult : IStructuredResult
    {
        /// <summary>
        /// Proposed dimensions, most important first.
        /// </summary>
        [Documentation("6 to 12 dimensions, most important first. Empty only when outOfScopeReason is set.")]
        public List<ProposedDimension> Dimensions { get; set; } = new List<ProposedDimension>();

        /// <summary>
        /// Why the request is not a topic to follow; null for a valid topic.
        /// </summary>
        [Documentation("Set only when the request is a task (write code, draft a document, answer a one-off question) rather than a topic to follow.")]
        public string? OutOfScopeReason { get; set; } = null;

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (!String.IsNullOrWhiteSpace(OutOfScopeReason)) return errors;
            if (Dimensions == null || Dimensions.Count == 0) errors.Add("dimensions must contain at least one dimension unless outOfScopeReason is set.");
            else
            {
                if (Dimensions.Count > 12) Dimensions = Dimensions.Take(12).ToList();
                for (int i = 0; i < Dimensions.Count; i++)
                    if (String.IsNullOrWhiteSpace(Dimensions[i].Name)) errors.Add("dimensions[" + i + "].name must not be empty.");
            }
            return errors;
        }
    }
}
