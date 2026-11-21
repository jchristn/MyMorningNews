namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// An emailed account link (email verification or password reset) cannot be used. <see cref="Code"/> tells why, so
    /// clients can say the right thing: LinkUsed means the link worked before (for verification the address is already
    /// confirmed, so the reader can sign in), LinkExpired means it is too old, and LinkInvalid means it is not a link
    /// this server issued for that purpose. The REST layer returns 400 with the code as the error.
    /// Derives from <see cref="ArgumentException"/> so callers that treat a bad link as a validation error keep working.
    /// </summary>
    public class AccountLinkException : ArgumentException
    {
        /// <summary>
        /// The link is not one this server issued for the purpose.
        /// </summary>
        public const string Invalid = "LinkInvalid";

        /// <summary>
        /// The link was already used.
        /// </summary>
        public const string Used = "LinkUsed";

        /// <summary>
        /// The link expired before it was used.
        /// </summary>
        public const string Expired = "LinkExpired";

        /// <summary>
        /// Error code: <see cref="Invalid"/>, <see cref="Used"/>, or <see cref="Expired"/>.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="code">Error code: <see cref="Invalid"/>, <see cref="Used"/>, or <see cref="Expired"/>.</param>
        /// <param name="message">Message shown to the caller.</param>
        /// <exception cref="ArgumentNullException">code is null or empty.</exception>
        public AccountLinkException(string code, string message) : base(message)
        {
            if (String.IsNullOrEmpty(code)) throw new ArgumentNullException(nameof(code));
            Code = code;
        }
    }
}
