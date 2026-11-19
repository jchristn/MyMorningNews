namespace MyMorningNews.Core.Exceptions
{
    /// <summary>
    /// The model did not produce valid structured output within the allowed repair turns.
    /// </summary>
    public class StructuredOutputException : ModelCallException
    {
        /// <summary>
        /// Last raw output, for diagnostics.
        /// </summary>
        public string? LastOutput { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="lastOutput">Last raw output.</param>
        public StructuredOutputException(string message, string? lastOutput) : base(message)
        {
            LastOutput = lastOutput;
        }
    }
}
