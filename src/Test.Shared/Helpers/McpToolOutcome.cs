namespace Test.Shared.Helpers
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// The result of an MCP tool call made by a test.
    /// </summary>
    public class McpToolOutcome
    {
        /// <summary>
        /// True when the tool reported an error (isError).
        /// </summary>
        public bool IsError { get; set; } = false;

        /// <summary>
        /// Text of the first content block.
        /// </summary>
        public string Text { get; set; } = String.Empty;

        /// <summary>
        /// Structured content, or an undefined element when absent.
        /// </summary>
        public JsonElement Structured { get; set; }

        /// <summary>
        /// A string property of the structured content, or null.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>Value or null.</returns>
        public string? Str(string name)
        {
            return Structured.ValueKind == JsonValueKind.Object && Structured.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        }
    }
}
