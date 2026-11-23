namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create a public share link for a briefing.
    /// </summary>
    public class BriefingShareCreateRequest
    {
        /// <summary>
        /// Days until the links stop working: 1 up to the server maximum (sharing.maxExpiryDays, 14 by default).
        /// Null uses the server default (sharing.defaultExpiryDays, 7 by default). A value outside the range is refused.
        /// </summary>
        public int? ExpiresInDays { get; set; } = null;

        /// <summary>
        /// Show my display name on the public pages. Default false. The email address is never shown.
        /// </summary>
        public bool? ShowOwnerName { get; set; } = null;
    }
}
