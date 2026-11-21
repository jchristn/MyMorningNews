namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Update a user. Null properties are unchanged.
    /// </summary>
    public class UserUpdateRequest
    {
        /// <summary>
        /// Email.
        /// </summary>
        [TextLimit(256)]
        public string? Email { get; set; } = null;

        /// <summary>
        /// New password (administrator reset).
        /// </summary>
        public string? Password { get; set; } = null;

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
        /// Global administrator.
        /// </summary>
        public bool? IsAdmin { get; set; } = null;

        /// <summary>
        /// Tenant administrator.
        /// </summary>
        public bool? IsTenantAdmin { get; set; } = null;

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

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
