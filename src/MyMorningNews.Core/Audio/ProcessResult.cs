namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// Exit code and output of an external process (ffmpeg).
    /// </summary>
    public class ProcessResult
    {
        /// <summary>
        /// Exit code.
        /// </summary>
        public int ExitCode { get; set; } = 0;

        /// <summary>
        /// Standard error.
        /// </summary>
        public string Error { get; set; } = String.Empty;

        /// <summary>
        /// Standard output.
        /// </summary>
        public string Output { get; set; } = String.Empty;
    }
}
