namespace MyMorningNews.McpServer.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;

    /// <summary>
    /// A Markdown resource: a fixed URI or a URI template, and a reader that renders it with the caller's client.
    /// </summary>
    public class McpResourceSpec
    {
        /// <summary>
        /// URI, or URI template when <see cref="IsTemplate"/> is true.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// Resource name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// True for a URI template such as <c>mmn://briefings/{id}</c>.
        /// </summary>
        public bool IsTemplate { get; }

        /// <summary>
        /// Reader: client, template variables (empty for fixed URIs), and a token. Returns Markdown.
        /// </summary>
        public Func<MyMorningNewsClient, IReadOnlyDictionary<string, string>, CancellationToken, Task<string>> Read { get; }

        /// <summary>
        /// Define a resource.
        /// </summary>
        /// <param name="uri">URI or template.</param>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="isTemplate">Whether the URI is a template.</param>
        /// <param name="read">Reader.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public McpResourceSpec(string uri, string name, string description, bool isTemplate, Func<MyMorningNewsClient, IReadOnlyDictionary<string, string>, CancellationToken, Task<string>> read)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            IsTemplate = isTemplate;
            Read = read ?? throw new ArgumentNullException(nameof(read));
        }
    }
}
