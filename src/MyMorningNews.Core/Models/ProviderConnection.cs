namespace MyMorningNews.Core.Models
{
    using System;
    using System.Text.Json.Serialization;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Private per-account model or speech connection. Secrets are encrypted and excluded from all API projections.
    /// </summary>
    public class ProviderConnection
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix pco_).
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
        /// Owning user identifier.
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
        /// Chat, Embedding, or Speech.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Kind
        {
            get
            {
                return _Kind;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Kind));
                _Kind = value;
            }
        }

        /// <summary>
        /// Supported API provider or Local speech.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Provider
        {
            get
            {
                return _Provider;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Provider));
                _Provider = value;
            }
        }

        /// <summary>
        /// Optional provider URL; local speech uses operator configuration. May be null.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Provider model identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Model
        {
            get
            {
                return _Model;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Model));
                _Model = value;
            }
        }

        /// <summary>
        /// Speech voice identifier. May be null.
        /// </summary>
        public string? Voice { get; set; } = null;

        /// <summary>
        /// Encrypted secret. Never serialized. May be null.
        /// </summary>
        [JsonIgnore]
        public string? SecretEncrypted { get; set; } = null;

        /// <summary>
        /// Masked display suffix. May be null.
        /// </summary>
        public string? SecretLast4 { get; set; } = null;

        /// <summary>
        /// Successful connection test time, cleared on change. May be null.
        /// </summary>
        public DateTime? ValidatedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateProviderConnectionId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Kind = String.Empty;
        private string _Provider = String.Empty;
        private string _Model = String.Empty;

        #endregion
    }
}
