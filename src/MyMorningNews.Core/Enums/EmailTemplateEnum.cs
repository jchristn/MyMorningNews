namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Email template.
    /// </summary>
    public enum EmailTemplateEnum
    {
        /// <summary>
        /// After a research run.
        /// </summary>
        RunDigest,

        /// <summary>
        /// An edition or one-time briefing.
        /// </summary>
        BriefingPublished,

        /// <summary>
        /// A run failed (opted-in administrators).
        /// </summary>
        RunFailed,

        /// <summary>
        /// Learned preference proposals.
        /// </summary>
        PreferenceProposal,

        /// <summary>
        /// Test email.
        /// </summary>
        Test
    }
}
