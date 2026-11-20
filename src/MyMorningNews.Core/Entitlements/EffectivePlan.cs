namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// The plan a user has right now and its entitlement values.
    /// </summary>
    public class EffectivePlan
    {
        /// <summary>
        /// Plan.
        /// </summary>
        public Plan Plan { get; set; } = new Plan();

        /// <summary>
        /// Entitlement values by key.
        /// </summary>
        public Dictionary<string, string> Features { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The assignment that gives it, or null for the default plan.
        /// </summary>
        public PlanAssignment? Assignment { get; set; } = null;

        /// <summary>
        /// When it ends, or null.
        /// </summary>
        public DateTime? EndsUtc { get; set; } = null;

        /// <summary>
        /// Assignments that start later (stacked codes).
        /// </summary>
        public List<PlanAssignment> Upcoming { get; set; } = new List<PlanAssignment>();

        /// <summary>
        /// Plan names by plan ID for the current and upcoming assignments.
        /// </summary>
        public Dictionary<string, string> PlanNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
