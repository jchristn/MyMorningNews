namespace MyMorningNews.Sdk.TestHarness
{
    /// <summary>
    /// Outcome of a harness case.
    /// </summary>
    public enum TestOutcomeEnum
    {
        /// <summary>
        /// Every assertion held.
        /// </summary>
        Pass,

        /// <summary>
        /// An assertion failed or an unexpected error occurred.
        /// </summary>
        Fail,

        /// <summary>
        /// Not run (needs --with-run, or the server lacks the feature).
        /// </summary>
        Skip
    }
}
