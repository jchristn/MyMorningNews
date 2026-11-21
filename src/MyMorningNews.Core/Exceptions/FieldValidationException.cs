namespace MyMorningNews.Core.Exceptions
{
    using System;

    /// <summary>
    /// A request value is invalid, and the field is known. Maps to 400 ValidationFailed with details.field, so forms can
    /// show the message next to the field.
    /// </summary>
    public class FieldValidationException : ArgumentException
    {
        /// <summary>
        /// Request field, in the JSON casing clients send (for example secret, model, baseUrl).
        /// </summary>
        public string Field { get; }

        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="field">Request field.</param>
        /// <param name="message">Message for the user.</param>
        public FieldValidationException(string field, string message) : base(message)
        {
            Field = field ?? String.Empty;
        }
    }
}
