namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// How a model endpoint signs in to its provider.
    /// </summary>
    public enum ModelAuthMethodEnum
    {
        /// <summary>
        /// A provider API key stored in the vault.
        /// </summary>
        ApiKey,

        /// <summary>
        /// A long-lived access token stored in the vault, such as the subscription token from claude setup-token.
        /// </summary>
        AccessToken,

        /// <summary>
        /// A sign-in file stored in the vault and written to a private folder for each run, such as Codex's auth.json,
        /// Gemini CLI's oauth_creds.json, or Mux's endpoints.json.
        /// </summary>
        AuthFile,

        /// <summary>
        /// The sign-in already on the server, made with the CLI's own login command inside the server container.
        /// </summary>
        ServerSignIn,

        /// <summary>
        /// No credentials, such as a local Ollama server.
        /// </summary>
        None
    }
}
