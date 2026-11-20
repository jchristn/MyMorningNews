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
        Test,

        /// <summary>
        /// Verify the email address after sign-up.
        /// </summary>
        VerifyEmail,

        /// <summary>
        /// Welcome after verification.
        /// </summary>
        Welcome,

        /// <summary>
        /// Password reset link.
        /// </summary>
        PasswordReset,

        /// <summary>
        /// The password was changed.
        /// </summary>
        PasswordChanged,

        /// <summary>
        /// Account deletion scheduled.
        /// </summary>
        AccountDeletion
    }
}
