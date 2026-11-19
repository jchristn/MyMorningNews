namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A web search source: SearXNG, Tavily, You.com, RSS, Hacker News, or arXiv.
    /// </summary>
    public class SearchProvider
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix spv_).
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
        /// Name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Kind.
        /// </summary>
        public SearchProviderKindEnum Kind { get; set; } = SearchProviderKindEnum.Searxng;

        /// <summary>
        /// Base URL (SearXNG, or an override for hosted APIs). May be null.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Vault credential holding the API key. May be null.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Used for topics that do not choose providers.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Maximum results per query. Minimum 1, maximum 50; values outside the range are clamped.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = Math.Clamp(value, 1, 50);
            }
        }

        /// <summary>
        /// Request timeout in milliseconds. Minimum 1000, maximum 120000; values outside the range are clamped.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                _TimeoutMs = Math.Clamp(value, 1000, 120000);
            }
        }

        /// <summary>
        /// UTC time of the last test. May be null.
        /// </summary>
        public DateTime? LastTestedUtc { get; set; } = null;

        /// <summary>
        /// Whether the last test succeeded. May be null.
        /// </summary>
        public bool? LastTestOk { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateSearchProviderId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private int _MaxResults = 10;
        private int _TimeoutMs = 15000;

        #endregion
    }
}
