namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// A referenced vault credential is missing, expired, or cannot be decrypted (for example after the signing key changed).
    /// </summary>
    public class VaultSecretUnavailableException : InvalidOperationException
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        public VaultSecretUnavailableException(string message) : base(message)
        {
        }
    }
}
