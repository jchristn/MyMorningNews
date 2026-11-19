namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Search;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Dependencies the stages share, plus the model call helper that renders a prompt for the run's topic and attributes
    /// the call to the run, topic, user, and prompt version.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class PipelineServices
    {
        #region Public-Members

        /// <summary>
        /// Database.
        /// </summary>
        public DatabaseDriverBase Database { get; }

        /// <summary>
        /// Model gateway.
        /// </summary>
        public ModelGateway Models { get; }

        /// <summary>
        /// Prompts.
        /// </summary>
        public PromptService Prompts { get; }

        /// <summary>
        /// Search.
        /// </summary>
        public SearchService Search { get; }

        /// <summary>
        /// Fetch.
        /// </summary>
        public FetchService Fetch { get; }

        /// <summary>
        /// Server settings.
        /// </summary>
        public ServerSettings Settings { get; }

        /// <summary>
        /// Logging.
        /// </summary>
        public LoggingModule Logging { get; }

        /// <summary>
        /// Briefing service for run briefings; null skips the ComposeBriefing stage.
        /// </summary>
        public BriefingService? Briefings { get; set; } = null;

        /// <summary>
        /// Email service for run digests; null skips the Notify stage.
        /// </summary>
        public Email.EmailService? Email { get; set; } = null;

        /// <summary>
        /// Learning service for feedback exemplars and active preferences in prompts; null leaves them empty.
        /// </summary>
        public MyMorningNews.Core.Feedback.LearningService? Learning { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="prompts">Prompts.</param>
        /// <param name="search">Search.</param>
        /// <param name="fetch">Fetch.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PipelineServices(DatabaseDriverBase database, ModelGateway models, PromptService prompts, SearchService search, FetchService fetch, ServerSettings settings, LoggingModule logging)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            Models = models ?? throw new ArgumentNullException(nameof(models));
            Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            Search = search ?? throw new ArgumentNullException(nameof(search));
            Fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Render a prompt with the run's base values plus stage values and make a structured call attributed to the run.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="ctx">Run context.</param>
        /// <param name="key">Prompt key.</param>
        /// <param name="values">Stage values (override base values of the same name).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Structured result.</returns>
        public async Task<StructuredCallResult<T>> CallAsync<T>(RunContext ctx, string key, Dictionary<string, object?> values, CancellationToken token) where T : class, IStructuredResult
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(values);
            Dictionary<string, object?> all = new Dictionary<string, object?>(ctx.BaseValues, StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> kv in values) all[kv.Key] = kv.Value;
            ResolvedPrompt prompt = await Prompts.ResolveAsync(ctx.Run.TenantId, key, ctx.Topic.Id, token).ConfigureAwait(false);
            RenderedPrompt rendered = PromptService.Render(prompt.Definition, all);
            return await Models.ChatStructuredAsync<T>(new ModelCallRequest
            {
                TenantId = ctx.Run.TenantId,
                UserId = ctx.Run.UserId,
                TopicId = ctx.Topic.Id,
                RunId = ctx.Run.Id,
                Task = prompt.Definition.Role,
                PromptKey = key,
                PromptTemplateVersionId = prompt.Version.Id,
                SystemPrompt = rendered.System,
                UserPrompt = rendered.User,
                IsDryRun = ctx.Run.IsDryRun
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Findings formatted for prompts: one line each with tier, score, title, and summary.
        /// </summary>
        /// <param name="findings">Findings.</param>
        /// <param name="max">Maximum lines.</param>
        /// <returns>Lines.</returns>
        public static List<string> FormatFindings(IEnumerable<Finding> findings, int max)
        {
            return findings.Take(Math.Max(1, max)).Select(f => "[" + f.Tier + ", " + f.RelevanceScore.ToString("0", CultureInfo.InvariantCulture) + "] " + f.Title +
                (String.IsNullOrWhiteSpace(f.Summary) ? String.Empty : ": " + f.Summary)).ToList();
        }

        /// <summary>
        /// Find a finding by title (case-insensitive exact, then prefix) among the run's findings.
        /// </summary>
        /// <param name="findings">Findings.</param>
        /// <param name="title">Title the model cited.</param>
        /// <returns>Finding, or null.</returns>
        public static Finding? MatchFinding(IEnumerable<Finding> findings, string? title)
        {
            if (String.IsNullOrWhiteSpace(title)) return null;
            string t = title.Trim().Trim('"');
            List<Finding> list = findings.ToList();
            return list.FirstOrDefault(f => String.Equals(f.Title, t, StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(f => f.Title.StartsWith(t, StringComparison.OrdinalIgnoreCase) || t.StartsWith(f.Title, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Word-set similarity of two texts (Jaccard over lowercase words of three or more letters), 0 to 1.
        /// </summary>
        /// <param name="a">First text.</param>
        /// <param name="b">Second text.</param>
        /// <returns>Similarity.</returns>
        public static double WordSimilarity(string? a, string? b)
        {
            HashSet<string> wa = Words(a);
            HashSet<string> wb = Words(b);
            if (wa.Count == 0 || wb.Count == 0) return 0;
            int inter = wa.Count(w => wb.Contains(w));
            return (double)inter / (wa.Count + wb.Count - inter);
        }

        #endregion

        #region Private-Methods

        private static HashSet<string> Words(string? text)
        {
            return new HashSet<string>((text ?? String.Empty).ToLowerInvariant().Split(new[] { ' ', ',', '.', ':', ';', '!', '?', '"', '\'', '(', ')', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3), StringComparer.Ordinal);
        }

        #endregion
    }
}
