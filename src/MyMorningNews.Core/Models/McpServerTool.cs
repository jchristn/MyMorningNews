namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A tool discovered on a registered MCP server.
    /// </summary>
    public class McpServerTool
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix mct_).
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
        /// Server.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string McpServerId
        {
            get
            {
                return _McpServerId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(McpServerId));
                _McpServerId = value;
            }
        }

        /// <summary>
        /// Tool name as the server lists it.
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
        /// Tool description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// JSON Schema of the tool arguments, as the server sent it. May be null.
        /// </summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>
        /// Offered to agentic research.
        /// </summary>
        public bool Enabled { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateMcpServerToolId();
        private string _TenantId = String.Empty;
        private string _McpServerId = String.Empty;
        private string _Name = String.Empty;

        #endregion
    }
}
