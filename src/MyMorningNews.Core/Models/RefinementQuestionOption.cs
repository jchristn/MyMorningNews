namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// An option for a choice question.
    /// </summary>
    public class RefinementQuestionOption
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rfo_).
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
        /// Question.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string QuestionId
        {
            get
            {
                return _QuestionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(QuestionId));
                _QuestionId = value;
            }
        }

        /// <summary>
        /// Label.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Label
        {
            get
            {
                return _Label;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Label));
                _Label = value;
            }
        }

        /// <summary>
        /// Value. May be null.
        /// </summary>
        public string? Value { get; set; } = null;

        /// <summary>
        /// Selected by the reader.
        /// </summary>
        public bool Selected { get; set; } = false;

        /// <summary>
        /// Order.
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

        private string _Id = IdGenerator.GenerateRefinementQuestionOptionId();
        private string _TenantId = String.Empty;
        private string _QuestionId = String.Empty;
        private string _Label = String.Empty;

        #endregion
    }
}
