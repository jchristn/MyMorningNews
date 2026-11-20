namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A tool offered to a model in an agentic loop (<see cref="ModelGateway.ChatAgenticAsync{T}"/>): its name,
    /// description, JSON Schema for the arguments, and the code that runs it.
    /// </summary>
    public class AgentTool
    {
        /// <summary>
        /// Tool name (letters, digits, and underscores; at most 64 characters).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description shown to the model.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// JSON Schema of the arguments (an object schema).
        /// </summary>
        public Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Runs the tool with the raw JSON arguments and returns the text given back to the model. Exceptions are caught
        /// by the loop and returned to the model as an error message.
        /// </summary>
        public Func<string, CancellationToken, Task<string>> Execute { get; }

        /// <summary>
        /// Define a tool.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="parameters">Arguments schema.</param>
        /// <param name="execute">Executor.</param>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public AgentTool(string name, string description, Dictionary<string, object> parameters, Func<string, CancellationToken, Task<string>> execute)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrWhiteSpace(description)) throw new ArgumentNullException(nameof(description));
            Name = name;
            Description = description;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
    }
}
