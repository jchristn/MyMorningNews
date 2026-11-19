namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// The operation conflicts with the current state of a resource (for example confirming a request that already ran).
    /// The REST layer returns 409 with the message.
    /// </summary>
    public class ConflictException : Exception
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message shown to the caller.</param>
        public ConflictException(string message) : base(message)
        {
        }
    }
}
