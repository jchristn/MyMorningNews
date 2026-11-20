namespace MyMorningNews.Benchmark
{
    /// <summary>
    /// Process exit codes shared by every command (BENCHMARKING.md "Commands and Exit Codes"). Scripts and CI depend on them.
    /// </summary>
    public static class ExitCodes
    {
        /// <summary>
        /// Success.
        /// </summary>
        public const int Success = 0;

        /// <summary>
        /// Error, validation failure, or a regression detected by compare.
        /// </summary>
        public const int Failure = 1;

        /// <summary>
        /// Unknown command or bad usage.
        /// </summary>
        public const int Usage = 2;

        /// <summary>
        /// Incomplete run: some input failed to load, so the results are not comparable.
        /// </summary>
        public const int Incomplete = 3;

        /// <summary>
        /// Cancelled with Ctrl+C.
        /// </summary>
        public const int Cancelled = 130;
    }
}
