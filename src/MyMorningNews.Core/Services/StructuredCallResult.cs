namespace MyMorningNews.Core.Services
{
    /// <summary>
    /// Structured result of a model call.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    public class StructuredCallResult<T> : ModelCallResult where T : class
    {
        /// <summary>
        /// Parsed and validated value.
        /// </summary>
        public T Value { get; set; } = null!;
    }
}
