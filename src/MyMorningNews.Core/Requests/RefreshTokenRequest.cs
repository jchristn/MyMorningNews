namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Exchange a mobile refresh token for a new session token and a rotated refresh token.
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// Refresh token returned by sign-in or the previous refresh.
        /// </summary>
        public string RefreshToken { get; set; } = String.Empty;
    }
}
