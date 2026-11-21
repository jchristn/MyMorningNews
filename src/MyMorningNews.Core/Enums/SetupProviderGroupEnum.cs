namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// How a model provider is reached, for grouping choices on the Setup page.
    /// </summary>
    public enum SetupProviderGroupEnum
    {
        /// <summary>
        /// A hosted model API used with an API key.
        /// </summary>
        Api,

        /// <summary>
        /// An agent CLI in the server image, used with a subscription sign-in or an API key.
        /// </summary>
        AgentCli,

        /// <summary>
        /// A model server you run yourself, such as Ollama.
        /// </summary>
        SelfHosted
    }
}
