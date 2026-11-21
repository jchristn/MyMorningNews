namespace MyMorningNews.Core.Responses
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One way a provider can sign in, as offered on the Setup page.
    /// </summary>
    public class SetupAuthOption
    {
        /// <summary>
        /// Method.
        /// </summary>
        public ModelAuthMethodEnum Method { get; set; } = ModelAuthMethodEnum.ApiKey;

        /// <summary>
        /// What the user supplies: Text (a key or token), File (a sign-in file's contents), or None.
        /// </summary>
        public string Input { get; set; } = "Text";

        /// <summary>
        /// File name the CLI keeps the sign-in in, for File inputs (auth.json, oauth_creds.json, endpoints.json).
        /// </summary>
        public string? FileName { get; set; } = null;

        /// <summary>
        /// Whether this is the suggested method for the provider.
        /// </summary>
        public bool Recommended { get; set; } = false;
    }
}
