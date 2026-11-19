namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// A bundled tool shown on the dashboard External Services card.
    /// </summary>
    public class ExternalServiceInfo
    {
        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Browser-reachable URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Default credentials, when any.
        /// </summary>
        public string? Credentials { get; set; } = null;

        /// <summary>
        /// Purpose.
        /// </summary>
        public string? Description { get; set; } = null;
    }
}
