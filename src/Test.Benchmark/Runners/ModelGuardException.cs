namespace MyMorningNews.Benchmark.Runners
{
    using System;

    /// <summary>
    /// A model call went to an endpoint the run was not allowed to use (for example a real provider during a stub run).
    /// The command stops at once.
    /// </summary>
    public class ModelGuardException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        public ModelGuardException(string message) : base(message)
        {
        }
    }
}
