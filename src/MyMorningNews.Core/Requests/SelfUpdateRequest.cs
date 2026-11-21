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
        [TextLimit(128)]
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name.
        /// </summary>
        [TextLimit(128)]
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Time zone.
        /// </summary>
        [TextLimit(64)]
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Locale.
        /// </summary>
        [TextLimit(16)]
        public string? Locale { get; set; } = null;
    }
}
