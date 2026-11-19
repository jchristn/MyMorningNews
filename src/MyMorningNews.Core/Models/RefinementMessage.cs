namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A message in a refinement session transcript.
    /// </summary>
    public class RefinementMessage
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rfm_).
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
        /// Session.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SessionId
        {
            get
            {
                return _SessionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SessionId));
                _SessionId = value;
            }
        }

        /// <summary>
        /// Role.
        /// </summary>
        public MessageRoleEnum Role { get; set; } = MessageRoleEnum.Assistant;

        /// <summary>
        /// Content.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Content
        {
            get
            {
                return _Content;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Content));
                _Content = value;
            }
        }

        /// <summary>
        /// Model call that produced it. May be null.
        /// </summary>
        public string? LlmCallId { get; set; } = null;

        /// <summary>
        /// Order in the session.
        /// </summary>
        public int Sequence { get; set; } = 0;

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

        private string _Id = IdGenerator.GenerateRefinementMessageId();
        private string _TenantId = String.Empty;
        private string _SessionId = String.Empty;
        private string _Content = String.Empty;

        #endregion
    }
}
