namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// Response to credential creation. SecretKey is returned exactly once.
    /// </summary>
    public class CredentialCreatedResponse
    {
        /// <summary>
        /// Credential.
        /// </summary>
        public CredentialResponse Credential { get; set; } = new CredentialResponse();

        /// <summary>
        /// Raw secret key. Store it now; it cannot be retrieved again.
        /// </summary>
        public string SecretKey { get; set; } = String.Empty;
    }
}
