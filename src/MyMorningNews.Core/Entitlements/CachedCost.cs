namespace MyMorningNews.Core.Entitlements
{
    using System;

    /// <summary>
    /// A cached month-to-date cost for one user.
    /// </summary>
    public class CachedCost
    {
        /// <summary>
        /// Cost in USD.
        /// </summary>
        public decimal CostUsd { get; }

        /// <summary>
        /// When the value must be read again (UTC).
        /// </summary>
        public DateTime ExpiresUtc { get; }

        /// <summary>
        /// Create an entry.
        /// </summary>
        /// <param name="costUsd">Cost.</param>
        /// <param name="expiresUtc">Expiry.</param>
        public CachedCost(decimal costUsd, DateTime expiresUtc)
        {
            CostUsd = costUsd;
            ExpiresUtc = expiresUtc;
        }
    }
}
