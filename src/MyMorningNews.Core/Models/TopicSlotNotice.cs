namespace MyMorningNews.Core.Models
{
    using System;

    /// <summary>
    /// Whether an account was told that it has more recurring custom topics than its plan allows. A stored row means
    /// the owner was notified for the current over-limit period; the row is removed once the account is back within
    /// the limit, so going over again notifies again.
    /// </summary>
    public class TopicSlotNotice
    {
        private long _OverLimit = 0;
        private long _TopicLimit = 0;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account owner.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Topics past the limit when the owner was notified. Default 0, minimum 0.
        /// </summary>
        public long OverLimit
        {
            get { return _OverLimit; }
            set { _OverLimit = Math.Max(0, value); }
        }

        /// <summary>
        /// The custom topic limit when the owner was notified. Default 0, minimum 0.
        /// </summary>
        public long TopicLimit
        {
            get { return _TopicLimit; }
            set { _TopicLimit = Math.Max(0, value); }
        }

        /// <summary>
        /// When the owner was notified, UTC, or null when no notice is recorded for the account.
        /// </summary>
        public DateTime? NotifiedUtc { get; set; } = null;
    }
}
