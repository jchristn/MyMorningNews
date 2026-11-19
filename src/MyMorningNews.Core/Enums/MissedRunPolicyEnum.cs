namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Handling of runs missed while the server was down.
    /// </summary>
    public enum MissedRunPolicyEnum
    {
        /// <summary>
        /// Skip missed runs.
        /// </summary>
        Skip,

        /// <summary>
        /// Run once on recovery.
        /// </summary>
        RunOnceOnRecovery
    }
}
