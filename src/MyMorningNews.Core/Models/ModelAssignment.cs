namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Assignment of a pipeline task role to a model endpoint, tenant-wide or for one topic.
    /// </summary>
    public class ModelAssignment
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix mda_).
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
        /// Task role.
        /// </summary>
        public ModelTaskEnum Task { get; set; } = ModelTaskEnum.Synthesis;

        /// <summary>
        /// Topic override; null for the tenant default. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Model endpoint.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ModelEndpointId
        {
            get
            {
                return _ModelEndpointId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ModelEndpointId));
                _ModelEndpointId = value;
            }
        }

        /// <summary>
        /// Model name overriding the endpoint default. May be null.
        /// </summary>
        public string? ModelOverride { get; set; } = null;

        /// <summary>
        /// Reasoning effort for this role. May be null.
        /// </summary>
        public ReasoningEffortEnum? ReasoningEffort { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateModelAssignmentId();
        private string _TenantId = String.Empty;
        private string _ModelEndpointId = String.Empty;

        #endregion
    }
}
