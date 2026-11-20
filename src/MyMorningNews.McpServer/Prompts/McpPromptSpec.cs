namespace MyMorningNews.McpServer.Prompts
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;
    using Voltaic.Mcp;

    /// <summary>
    /// A prompt: its name, description, arguments, and a builder that fills it with the caller's data.
    /// </summary>
    public class McpPromptSpec
    {
        /// <summary>
        /// Prompt name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Arguments.
        /// </summary>
        public List<McpPromptArgument> Arguments { get; }

        /// <summary>
        /// Builder: arguments, client, token. Returns the prompt messages.
        /// </summary>
        public Func<ToolArguments, MyMorningNewsClient, CancellationToken, Task<McpGetPromptResult>> Build { get; }

        /// <summary>
        /// Define a prompt.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="title">Human title.</param>
        /// <param name="description">Description.</param>
        /// <param name="arguments">Arguments.</param>
        /// <param name="build">Builder.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public McpPromptSpec(string name, string title, string description, List<McpPromptArgument> arguments, Func<ToolArguments, MyMorningNewsClient, CancellationToken, Task<McpGetPromptResult>> build)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            Build = build ?? throw new ArgumentNullException(nameof(build));
        }
    }
}
