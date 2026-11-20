namespace MyMorningNews.Sdk.TestHarness
{
    using System;

    /// <summary>
    /// An assertion in a harness case did not hold.
    /// </summary>
    public class HarnessAssertionException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">What did not hold.</param>
        public HarnessAssertionException(string message) : base(message)
        {
        }
    }
}
