namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A clarifying question asked during refinement, with the reader answer.
    /// </summary>
    public class RefinementQuestion
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rfq_).
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
        /// Question round.
        /// </summary>
        public int Round { get; set; } = 1;

        /// <summary>
        /// Dimension it concerns, when any. May be null.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Question.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string QuestionText
        {
            get
            {
                return _QuestionText;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(QuestionText));
                _QuestionText = value;
            }
        }

        /// <summary>
        /// Why it is asked. May be null.
        /// </summary>
        public string? Rationale { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public QuestionKindEnum Kind { get; set; } = QuestionKindEnum.SingleChoice;

        /// <summary>
        /// Free-text or scale answer. May be null.
        /// </summary>
        public string? AnswerText { get; set; } = null;

        /// <summary>
        /// Answered.
        /// </summary>
        public bool Answered { get; set; } = false;

        /// <summary>
        /// Skipped by the reader.
        /// </summary>
        public bool Skipped { get; set; } = false;

        /// <summary>
        /// Order within the round.
        /// </summary>
        public int SortOrder { get; set; } = 0;

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

        private string _Id = IdGenerator.GenerateRefinementQuestionId();
        private string _TenantId = String.Empty;
        private string _SessionId = String.Empty;
        private string _QuestionText = String.Empty;

        #endregion
    }
}
