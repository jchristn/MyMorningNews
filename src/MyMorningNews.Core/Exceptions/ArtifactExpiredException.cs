namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// The stored file was deleted by retention or removed from the store. The REST layer returns 410 with the message.
    /// </summary>
    public class ArtifactExpiredException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message shown to the caller.</param>
        public ArtifactExpiredException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate with an inner exception.
        /// </summary>
        /// <param name="message">Message shown to the caller.</param>
        /// <param name="inner">Cause.</param>
        public ArtifactExpiredException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
