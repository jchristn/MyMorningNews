namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// Speech synthesis failed.
    /// </summary>
    public class TtsException : Exception
    {
        /// <summary>
        /// HTTP status from the provider, or 0.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="statusCode">HTTP status, or 0.</param>
        /// <param name="inner">Cause, or null.</param>
        public TtsException(string message, int statusCode = 0, Exception? inner = null) : base(message, inner)
        {
            StatusCode = statusCode;
        }
    }
}
