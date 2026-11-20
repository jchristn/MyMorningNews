namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A remote MCP server registered by the tenant; its tools can be offered to agentic research.
    /// </summary>
    public class McpServer
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix mcs_).
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
        /// Display name.
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
        /// Streamable HTTP or SSE endpoint URL.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Url
        {
            get
            {
                return _Url;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Url));
                _Url = value;
            }
        }

        /// <summary>
        /// Transport.
        /// </summary>
        public McpTransportEnum Transport { get; set; } = McpTransportEnum.StreamableHttp;

        /// <summary>
        /// Vault credential whose value is sent in the auth header; null for none. May be null.
        /// </summary>
        public string? VaultCredentialId { get; set; } = null;

        /// <summary>
        /// Header that carries the credential.
        /// </summary>
        public string HeaderName
        {
            get
            {
                return _HeaderName;
            }
            set
            {
                _HeaderName = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Text placed before the credential value, for example "Bearer ".
        /// </summary>
        public string HeaderPrefix
        {
            get
            {
                return _HeaderPrefix;
            }
            set
            {
                _HeaderPrefix = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Whether its tools may be used.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Tools found at the last discovery.
        /// </summary>
        public int ToolCount { get; set; } = 0;

        /// <summary>
        /// Last successful discovery. May be null.
        /// </summary>
        public DateTime? LastDiscoveredUtc { get; set; } = null;

        /// <summary>
        /// Last connection or discovery error. May be null.
        /// </summary>
        public string? LastError { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateMcpServerId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private string _Url = String.Empty;
        private string _HeaderName = "Authorization";
        private string _HeaderPrefix = "Bearer ";

        #endregion
    }
}
