namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Findings selected for a section, optionally under a group name.
    /// </summary>
    public class FindingGroup
    {
        /// <summary>
        /// Group name, or null when ungrouped.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Findings.
        /// </summary>
        public List<Finding> Items { get; set; } = new List<Finding>();
    }
}
