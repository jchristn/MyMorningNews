namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Create or update a plan. Null fields are unchanged on update.
    /// </summary>
    public class PlanUpsertRequest
    {
        /// <summary>
        /// Stable key (required on create).
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Name.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Entitlement values to set; an empty value removes the key.
        /// </summary>
        public Dictionary<string, string>? Features { get; set; } = null;
    }
}
