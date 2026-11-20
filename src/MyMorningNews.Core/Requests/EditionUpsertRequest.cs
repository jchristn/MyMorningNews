namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update an edition. Null fields are unchanged on update.
    /// </summary>
    public class EditionUpsertRequest
    {
        /// <summary>
        /// Name (required on create).
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Template; empty string clears it.
        /// </summary>
        public string? BriefingTemplateId { get; set; } = null;

        /// <summary>
        /// Produce audio.
        /// </summary>
        public bool? AudioEnabled { get; set; } = null;

        /// <summary>
        /// Push a notification to the owner's devices when the edition is ready.
        /// </summary>
        public bool? PushEnabled { get; set; } = null;

        /// <summary>
        /// Email the briefing.
        /// </summary>
        public bool? EmailEnabled { get; set; } = null;

        /// <summary>
        /// Compose on schedule.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Run topics first.
        /// </summary>
        public bool? FreshResearchBeforeCompose { get; set; } = null;

        /// <summary>
        /// Longest wait for fresh research (5 to 240).
        /// </summary>
        public int? FreshResearchLeadMinutes { get; set; } = null;

        /// <summary>
        /// Which findings are eligible.
        /// </summary>
        public LookbackModeEnum? LookbackMode { get; set; } = null;

        /// <summary>
        /// Lookback hours (1 to 2160).
        /// </summary>
        public int? LookbackHours { get; set; } = null;
    }
}
