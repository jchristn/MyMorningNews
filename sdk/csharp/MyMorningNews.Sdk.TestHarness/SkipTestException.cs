namespace MyMorningNews.Sdk.TestHarness
{
    using System;

    /// <summary>
    /// A harness case decided not to run (for example, it needs --with-run or a newer server).
    /// </summary>
    public class SkipTestException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="reason">Why the case was skipped.</param>
        public SkipTestException(string reason) : base(reason)
        {
        }
    }
}
