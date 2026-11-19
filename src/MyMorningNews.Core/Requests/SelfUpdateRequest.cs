namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Update one's own profile. Null properties are unchanged.
    /// </summary>
    public class SelfUpdateRequest
    {
        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Time zone.
        /// </summary>
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Locale.
        /// </summary>
        public string? Locale { get; set; } = null;
    }
}
