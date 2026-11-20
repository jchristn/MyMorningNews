namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Where a promotional code was redeemed.
    /// </summary>
    public enum PromoChannelEnum
    {
        /// <summary>
        /// At sign-up.
        /// </summary>
        Signup,

        /// <summary>
        /// From account settings or the app.
        /// </summary>
        Account,

        /// <summary>
        /// Applied by an administrator.
        /// </summary>
        Admin,

        /// <summary>
        /// Through the API.
        /// </summary>
        Api
    }
}
