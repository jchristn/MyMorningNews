namespace MyMorningNews.Core.Exceptions
{
    /// <summary>
    /// The model declined the request (stop reason refusal).
    /// </summary>
    public class ModelRefusalException : ModelCallException
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        public ModelRefusalException(string message) : base(message, 200, false)
        {
        }
    }
}
