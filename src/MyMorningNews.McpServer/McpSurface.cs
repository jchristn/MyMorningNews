namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.McpServer.Prompts;
    using MyMorningNews.McpServer.Resources;
    using MyMorningNews.McpServer.Tools;
    using MyMorningNews.Sdk;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// The MCP surface (tools, resources, and prompts) bound to a way of finding the caller's credentials. It registers
    /// the same surface on the stdio server and the Streamable HTTP server, maps REST errors to tool errors the model
    /// can read, and retries once with fresh credentials when a stdio session has expired. Thread safe.
    /// </summary>
    public class McpSurface
    {
        #region Public-Members

        /// <summary>
        /// Every tool.
        /// </summary>
        public IReadOnlyList<McpTool> Tools { get; }

        /// <summary>
        /// Every resource.
        /// </summary>
        public IReadOnlyList<McpResourceSpec> Resources { get; }

        /// <summary>
        /// Every prompt.
        /// </summary>
        public IReadOnlyList<McpPromptSpec> Prompts { get; }

        #endregion

        #region Private-Members

        private readonly ApiClientFactory _Clients;
        private readonly Func<CancellationToken, Task<CallerCredentials>> _Caller;
        private readonly Func<CancellationToken, Task<CallerCredentials>>? _Renew;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the surface.
        /// </summary>
        /// <param name="clients">Client factory.</param>
        /// <param name="caller">Finds the caller's credentials for the current call.</param>
        /// <param name="renew">Renews expired credentials (stdio sign-in), or null when they cannot be renewed.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public McpSurface(ApiClientFactory clients, Func<CancellationToken, Task<CallerCredentials>> caller, Func<CancellationToken, Task<CallerCredentials>>? renew)
        {
            _Clients = clients ?? throw new ArgumentNullException(nameof(clients));
            _Caller = caller ?? throw new ArgumentNullException(nameof(caller));
            _Renew = renew;
            Tools = AllTools();
            Resources = ResourceCatalog.Build();
            Prompts = PromptCatalog.Build();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Every tool, in the order clients list them.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> AllTools()
        {
            List<McpTool> tools = new List<McpTool>();
            tools.AddRange(TopicTools.Build());
            tools.AddRange(TopicEditTools.Build());
            tools.AddRange(FindingTools.Build());
            tools.AddRange(BriefingTools.Build());
            tools.AddRange(OutcomeTools.Build());
            tools.AddRange(LearningTools.Build());
            return tools;
        }

        /// <summary>
        /// The tool, resource, and prompt definitions as indented JSON, for documentation (MCP_API.md is generated from it).
        /// </summary>
        /// <returns>JSON.</returns>
        public static string DescribeJson()
        {
            Dictionary<string, object?> doc = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "server", McpHost.ServerName },
                { "version", McpHost.ServerVersion },
                { "tools", AllTools().Select(t => new Dictionary<string, object?>
                    {
                        { "name", t.Name }, { "title", t.Title }, { "description", t.Description }, { "readOnly", t.ReadOnly }, { "destructive", t.Destructive },
                        { "inputSchema", t.InputSchema }, { "outputSchema", t.OutputSchema }
                    }).ToList() },
                { "resources", ResourceCatalog.Build().Select(r => new Dictionary<string, object?> { { "uri", r.Uri }, { "name", r.Name }, { "description", r.Description }, { "template", r.IsTemplate } }).ToList() },
                { "prompts", PromptCatalog.Build().Select(p => new Dictionary<string, object?>
                    {
                        { "name", p.Name }, { "title", p.Title }, { "description", p.Description },
                        { "arguments", p.Arguments.Select(a => new Dictionary<string, object?> { { "name", a.Name }, { "description", a.Description }, { "required", a.Required == true } }).ToList() }
                    }).ToList() }
            };
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Credentials for an HTTP call, from the claims Voltaic attached after <see cref="CallerAuthenticator"/> ran.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credentials.</returns>
        /// <exception cref="McpToolException">The call was not authenticated.</exception>
        public static Task<CallerCredentials> FromCallContext(CancellationToken token)
        {
            CallerCredentials c = CallerCredentials.FromClaims(RpcCallContext.Current?.Claims);
            if (!c.IsComplete) throw new McpToolException("This call carries no My Morning News credentials.");
            return Task.FromResult(c);
        }

        /// <summary>
        /// Register the surface on the stdio server.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <exception cref="ArgumentNullException">Server is null.</exception>
        public void RegisterOn(McpServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            Register(server.RegisterTool, server.RegisterResource, server.RegisterResourceTemplate, server.RegisterPrompt);
        }

        /// <summary>
        /// Register the surface on the Streamable HTTP server.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <exception cref="ArgumentNullException">Server is null.</exception>
        public void RegisterOn(McpHttpServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            Register(server.RegisterTool, server.RegisterResource, server.RegisterResourceTemplate, server.RegisterPrompt);
        }

        /// <summary>
        /// Run one tool with the current caller's credentials. Used by the transports and by tests.
        /// </summary>
        /// <param name="tool">Tool.</param>
        /// <param name="parameters">Arguments.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Structured tool result.</returns>
        /// <exception cref="ArgumentNullException">Tool is null.</exception>
        /// <exception cref="McpToolException">The call failed; the message explains why.</exception>
        public async Task<object> InvokeToolAsync(McpTool tool, RpcParameters? parameters, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(tool);
            object result = await WithClientAsync(c => tool.Handler(new ToolArguments(parameters), c, token), token, ToolError).ConfigureAwait(false);
            return McpToolCallResult.FromStructured(result);
        }

        #endregion

        #region Private-Methods

        private void Register(
            Action<ToolDefinition, Func<RpcParameters?, CancellationToken, Task<object>>> registerTool,
            Action<McpResource, Func<string, CancellationToken, Task<McpReadResourceResult>>> registerResource,
            Action<McpResourceTemplate, Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<McpReadResourceResult>>> registerTemplate,
            Action<McpPrompt, Func<RpcParameters?, CancellationToken, Task<McpGetPromptResult>>> registerPrompt)
        {
            foreach (McpTool tool in Tools)
            {
                McpTool captured = tool;
                ToolDefinition definition = new ToolDefinition
                {
                    Name = tool.Name,
                    Title = tool.Title,
                    Description = tool.Description,
                    InputSchema = tool.InputSchema,
                    OutputSchema = tool.OutputSchema,
                    Annotations = new McpAnnotations { Title = tool.Title, ReadOnlyHint = tool.ReadOnly, DestructiveHint = tool.Destructive }
                };
                registerTool(definition, (p, t) => InvokeToolAsync(captured, p, t));
            }

            foreach (McpResourceSpec r in Resources)
            {
                McpResourceSpec captured = r;
                if (r.IsTemplate)
                {
                    McpResourceTemplate template = new McpResourceTemplate { UriTemplate = r.Uri, Name = r.Name, Title = r.Name, Description = r.Description, MimeType = "text/markdown" };
                    registerTemplate(template, (uri, vars, t) => ReadResourceAsync(captured, uri, vars, t));
                }
                else
                {
                    McpResource resource = new McpResource { Uri = r.Uri, Name = r.Name, Title = r.Name, Description = r.Description, MimeType = "text/markdown" };
                    registerResource(resource, (uri, t) => ReadResourceAsync(captured, uri, new Dictionary<string, string>(), t));
                }
            }

            foreach (McpPromptSpec p in Prompts)
            {
                McpPromptSpec captured = p;
                McpPrompt prompt = new McpPrompt { Name = p.Name, Title = p.Title, Description = p.Description, Arguments = p.Arguments };
                registerPrompt(prompt, (args, t) => WithClientAsync(c => captured.Build(new ToolArguments(args), c, t), t, ToolError));
            }
        }

        private async Task<McpReadResourceResult> ReadResourceAsync(McpResourceSpec spec, string uri, IReadOnlyDictionary<string, string> vars, CancellationToken token)
        {
            string markdown = await WithClientAsync(c => spec.Read(c, vars, token), token, ex => ResourceError(ex, uri)).ConfigureAwait(false);
            return new McpReadResourceResult { Contents = new List<object> { new McpTextResourceContents { Uri = uri, MimeType = "text/markdown", Text = markdown } } };
        }

        private async Task<T> WithClientAsync<T>(Func<MyMorningNewsClient, Task<T>> work, CancellationToken token, Func<MyMorningNewsException, Exception> map)
        {
            CallerCredentials credentials = await _Caller(token).ConfigureAwait(false);
            try
            {
                try
                {
                    using (MyMorningNewsClient client = _Clients.Create(credentials))
                    {
                        return await work(client).ConfigureAwait(false);
                    }
                }
                catch (MyMorningNewsException ex) when (ex.StatusCode == 401 && _Renew != null)
                {
                    credentials = await _Renew(token).ConfigureAwait(false);
                    using (MyMorningNewsClient client = _Clients.Create(credentials))
                    {
                        return await work(client).ConfigureAwait(false);
                    }
                }
            }
            catch (MyMorningNewsException ex)
            {
                throw map(ex);
            }
            catch (TimeoutException ex)
            {
                throw new McpToolException(ex.Message);
            }
            catch (ArgumentException ex)
            {
                throw new McpToolException(ex.Message);
            }
        }

        private static Exception ToolError(MyMorningNewsException ex)
        {
            return new McpToolException(Describe(ex));
        }

        private static Exception ResourceError(MyMorningNewsException ex, string uri)
        {
            if (ex.StatusCode == 404) return new McpProtocolException(-32002, "Resource '" + uri + "' was not found.", new Dictionary<string, object?> { { "uri", uri } });
            return new McpProtocolException(-32603, Describe(ex));
        }

        /// <summary>
        /// A readable description of a REST error, without secrets: status, error code, message, details, and request ID.
        /// </summary>
        /// <param name="ex">Exception.</param>
        /// <returns>Description.</returns>
        /// <exception cref="ArgumentNullException">Exception is null.</exception>
        public static string Describe(MyMorningNewsException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            string head = ex.StatusCode == 0 ? "The My Morning News API could not be reached" : "The My Morning News API returned " + ex.StatusCode + (String.IsNullOrEmpty(ex.ErrorCode) ? String.Empty : " " + ex.ErrorCode);
            string details = ex.Details.Any() ? " (" + String.Join("; ", ex.Details.Select(d => d.Key + ": " + d.Value)) + ")" : String.Empty;
            string request = String.IsNullOrEmpty(ex.RequestId) ? String.Empty : " Request " + ex.RequestId + ".";
            return head + ": " + ex.Message.TrimEnd().TrimEnd('.') + details + "." + request;
        }

        #endregion
    }
}
