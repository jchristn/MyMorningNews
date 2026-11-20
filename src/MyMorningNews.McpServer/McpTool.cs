namespace MyMorningNews.McpServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;

    /// <summary>
    /// One MCP tool: its definition (name, description, input and output schemas, hints) and a handler that calls the
    /// REST API through the SDK with the caller's credentials.
    /// </summary>
    public class McpTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name (snake case).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Short human title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Description shown to the model.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Input JSON schema.
        /// </summary>
        public object InputSchema { get; }

        /// <summary>
        /// Output JSON schema. Results are returned as structured content that matches it.
        /// </summary>
        public object OutputSchema { get; }

        /// <summary>
        /// True when the tool only reads.
        /// </summary>
        public bool ReadOnly { get; }

        /// <summary>
        /// True when the tool can remove or overwrite data.
        /// </summary>
        public bool Destructive { get; }

        /// <summary>
        /// Handler: arguments, an SDK client carrying the caller's credentials, and a cancellation token. Returns the
        /// structured result.
        /// </summary>
        public Func<ToolArguments, MyMorningNewsClient, CancellationToken, Task<object>> Handler { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Define a tool.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="title">Title.</param>
        /// <param name="description">Description.</param>
        /// <param name="inputSchema">Input schema.</param>
        /// <param name="outputSchema">Output schema.</param>
        /// <param name="readOnly">Read-only hint.</param>
        /// <param name="destructive">Destructive hint.</param>
        /// <param name="handler">Handler.</param>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public McpTool(string name, string title, string description, object inputSchema, object outputSchema, bool readOnly, bool destructive,
            Func<ToolArguments, MyMorningNewsClient, CancellationToken, Task<object>> handler)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));
            if (String.IsNullOrWhiteSpace(description)) throw new ArgumentNullException(nameof(description));
            Name = name;
            Title = title;
            Description = description;
            InputSchema = inputSchema ?? throw new ArgumentNullException(nameof(inputSchema));
            OutputSchema = outputSchema ?? throw new ArgumentNullException(nameof(outputSchema));
            ReadOnly = readOnly;
            Destructive = destructive;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        #endregion
    }
}
