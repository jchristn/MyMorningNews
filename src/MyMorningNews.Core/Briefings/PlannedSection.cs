namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A template section with the items selected for it and its written body.
    /// </summary>
    public class PlannedSection
    {
        /// <summary>
        /// Template section.
        /// </summary>
        public TemplateSection Section { get; set; } = new TemplateSection();

        /// <summary>
        /// Selected findings.
        /// </summary>
        public List<FindingGroup> Findings { get; set; } = new List<FindingGroup>();

        /// <summary>
        /// Selected predictions.
        /// </summary>
        public List<Prediction> Predictions { get; set; } = new List<Prediction>();

        /// <summary>
        /// Selected suggestions.
        /// </summary>
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();

        /// <summary>
        /// Selected runs.
        /// </summary>
        public List<Run> Runs { get; set; } = new List<Run>();

        /// <summary>
        /// Written body, or null when the section is omitted.
        /// </summary>
        public string? Body { get; set; } = null;

        /// <summary>
        /// Items covered.
        /// </summary>
        public int ItemCount
        {
            get { return Findings.Sum(g => g.Items.Count) + Predictions.Count + Suggestions.Count + Runs.Count; }
        }

        /// <summary>
        /// Whether the section has nothing to write (free-form sections are never empty).
        /// </summary>
        public bool IsEmpty
        {
            get { return Section.Directive.Source != "none" && ItemCount == 0; }
        }
    }
}
