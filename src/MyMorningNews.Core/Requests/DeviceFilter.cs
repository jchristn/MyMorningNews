namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Device records. Null properties are not applied.
    /// </summary>
    public class DeviceFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match ExpoPushToken exactly when set.
        /// </summary>
        public string? ExpoPushToken { get; set; } = null;

        /// <summary>
        /// Match Platform exactly when set.
        /// </summary>
        public string? Platform { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
