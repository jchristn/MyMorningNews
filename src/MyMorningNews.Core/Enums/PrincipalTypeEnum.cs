namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Authenticated principal type.
    /// </summary>
    public enum PrincipalTypeEnum
    {
        /// <summary>
        /// System administrator.
        /// </summary>
        Administrator,

        /// <summary>
        /// Interactive user.
        /// </summary>
        User,

        /// <summary>
        /// API credential.
        /// </summary>
        Credential
    }
}
