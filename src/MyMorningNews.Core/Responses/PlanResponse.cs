namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A plan with its entitlement values.
    /// </summary>
    public class PlanResponse
    {
        /// <summary>
        /// Plan.
        /// </summary>
        public Plan Plan { get; set; } = new Plan();

        /// <summary>
        /// Values by key.
        /// </summary>
        public Dictionary<string, string> Features { get; set; } = new Dictionary<string, string>();
    }
}
