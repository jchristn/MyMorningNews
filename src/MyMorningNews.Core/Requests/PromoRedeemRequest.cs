namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Redeem a promotional code.
    /// </summary>
    public class PromoRedeemRequest
    {
        /// <summary>
        /// Code.
        /// </summary>
        public string? Code { get; set; } = null;
    }
}
