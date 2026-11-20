namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Where a plan assignment came from.
    /// </summary>
    public enum PlanSourceEnum
    {
        /// <summary>
        /// The default plan.
        /// </summary>
        Default,

        /// <summary>
        /// Granted by an administrator.
        /// </summary>
        Admin,

        /// <summary>
        /// A promotional code.
        /// </summary>
        Promo,

        /// <summary>
        /// A paid subscription (later).
        /// </summary>
        Billing
    }
}
