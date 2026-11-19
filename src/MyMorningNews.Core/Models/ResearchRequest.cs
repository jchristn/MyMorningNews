namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A one-time request ("build me a podcast on X"): researched once, now or at a set time, and delivered as a report, podcast, or email.
    /// </summary>
    public class ResearchRequest
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rrq_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Owning user.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// One-time topic holding the brief and research context. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// What the reader asked for.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string RequestText
        {
            get
            {
                return _RequestText;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RequestText));
                _RequestText = value;
            }
        }

        /// <summary>
        /// Short title. May be null.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Report, Podcast, and or Email.
        /// </summary>
        public List<string> Deliverables
        {
            get
            {
                return _Deliverables;
            }
            set
            {
                _Deliverables = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Length.
        /// </summary>
        public DeliverableLengthEnum Length { get; set; } = DeliverableLengthEnum.Standard;

        /// <summary>
        /// Status.
        /// </summary>
        public ResearchRequestStatusEnum Status { get; set; } = ResearchRequestStatusEnum.Clarifying;

        /// <summary>
        /// When to run; null runs as soon as confirmed. May be null.
        /// </summary>
        public DateTime? RunAtUtc { get; set; } = null;

        /// <summary>
        /// Email recipients when Email is a deliverable.
        /// </summary>
        public List<string> EmailRecipients
        {
            get
            {
                return _EmailRecipients;
            }
            set
            {
                _EmailRecipients = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Also publish the podcast to the reader private feed.
        /// </summary>
        public bool PublishToFeed { get; set; } = false;

        /// <summary>
        /// Why the request was declined, when it is not a research request. May be null.
        /// </summary>
        public string? OutOfScopeReason { get; set; } = null;

        /// <summary>
        /// Estimated model cost shown before running. May be null.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Run that produced the result. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Resulting briefing. May be null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Recurring topic created from this request. May be null.
        /// </summary>
        public string? ConvertedTopicId { get; set; } = null;

        /// <summary>
        /// Failure reason. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// UTC time the reader confirmed the request. May be null.
        /// </summary>
        public DateTime? ConfirmedUtc { get; set; } = null;

        /// <summary>
        /// UTC completion time. May be null.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Whether the record is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateResearchRequestId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _RequestText = String.Empty;
        private List<string> _Deliverables = new List<string>();
        private List<string> _EmailRecipients = new List<string>();

        #endregion
    }
}
