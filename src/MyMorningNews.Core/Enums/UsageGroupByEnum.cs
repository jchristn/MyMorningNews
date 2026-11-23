namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Grouping for usage and cost reports.
    /// </summary>
    public enum UsageGroupByEnum
    {
        /// <summary>
        /// Attributed user.
        /// </summary>
        User,

        /// <summary>
        /// Model, keyed by endpoint and model name so the same model name on two endpoints is reported separately.
        /// </summary>
        Model,

        /// <summary>
        /// Task role.
        /// </summary>
        Task,

        /// <summary>
        /// Provider.
        /// </summary>
        Provider,

        /// <summary>
        /// Topic.
        /// </summary>
        Topic,

        /// <summary>
        /// Prompt key.
        /// </summary>
        PromptKey,

        /// <summary>
        /// Calendar day (UTC).
        /// </summary>
        Day,

        /// <summary>
        /// Tenant (platform-wide reports only).
        /// </summary>
        Tenant
    }
}
