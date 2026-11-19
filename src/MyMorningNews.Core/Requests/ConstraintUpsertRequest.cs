namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a topic constraint. Null fields are left unchanged on update.
    /// </summary>
    public class ConstraintUpsertRequest
    {
        /// <summary>
        /// Kind.
        /// </summary>
        public ConstraintKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Value.
        /// </summary>
        public string? Value { get; set; } = null;

        /// <summary>
        /// Note.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// Dimension.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Locked.
        /// </summary>
        public bool? IsLocked { get; set; } = null;
    }
}
