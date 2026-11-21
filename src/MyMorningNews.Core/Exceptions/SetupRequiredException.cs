namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// The workspace is missing something it needs, such as a working model key. Maps to 409 SetupRequired.
    /// </summary>
    public class SetupRequiredException : Exception
    {
        /// <summary>
        /// Setup check key that is not ready, for example model:mdl_abc.
        /// </summary>
        public string CheckKey { get; }

        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="checkKey">Setup check key.</param>
        /// <param name="message">Message for the user.</param>
        public SetupRequiredException(string checkKey, string message) : base(message)
        {
            CheckKey = checkKey ?? String.Empty;
        }
    }
}
