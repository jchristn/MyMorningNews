namespace MyMorningNews.Core.Mcp
{
    using System;

    /// <summary>
    /// A registered MCP server could not be reached, refused a request, or is misconfigured.
    /// </summary>
    public class McpConnectionException : Exception
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        public McpConnectionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create the exception with an inner exception.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="inner">Inner exception.</param>
        public McpConnectionException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
