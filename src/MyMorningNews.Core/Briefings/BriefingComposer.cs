namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Storage;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Composes briefings from templates (plan section 21.2): selects items per section directive, writes each section with
    /// briefing.section (bounded parallel), writes the opening with briefing.compose, numbers and resolves citations, and
    /// stores the Markdown, sections, items, and the Markdown and HTML files. When a model call fails, the section is
    /// written as a plain list so a briefing always arrives. Thread safety: safe for concurrent compositions.
    /// </summary>
    public class BriefingComposer
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ModelGateway _Models;
        private readonly PromptService _Prompts;
        private readonly ArtifactService _Artifacts;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;

        private static readonly Dictionary<string, int> _TierRank = new Dictionary<string, int>(StringComparer.Ordinal) { { "MustRead", 3 }, { "WorthALook", 2 }, { "Fyi", 1 } };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="prompts">Prompts.</param>
        /// <param name="artifacts">Artifact storage.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public BriefingComposer(DatabaseDriverBase database, ModelGateway models, PromptService prompts, ArtifactService artifacts, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            _Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Compose into the given briefing row. When scope.Save is true the row, sections, items, and files are saved and the
        /// row is marked Ready; otherwise only the returned object carries the Markdown (preview).
        /// </summary>
        /// <param name="briefing">Briefing row (created by the caller; Id used for attribution).</param>
        /// <param name="scope">Scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Composed briefing.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<Briefing> ComposeAsync(Briefing briefing, ComposeScope scope, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            ArgumentNullException.ThrowIfNull(scope);
            BriefingTemplateDefinition def = scope.Template.Definition;
            ComposeData data = await LoadAsync(scope, token).ConfigureAwait(false);
            List<string> comparisons = new List<string>();
            foreach (Topic topic in scope.Topics)
            {
                RunHistorySnapshot? snapshot = (await _Database.RunHistorySnapshots.EnumerateAsync(new RunHistorySnapshotFilter
                { TenantId = scope.TenantId, TopicId = topic.Id, RunId = scope.RunId, ToUtc = scope.PeriodEndUtc, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
                comparisons.Add("Topic: " + topic.Name + "\n" + HistoryContextService.Prompt(snapshot ?? new RunHistorySnapshot { Mode = "Off" }));
            }
            scope.HistoryInstructions = String.Join("\n\n", comparisons);
            Dictionary<string, string> vars = Variables(scope);
            string title = TemplateParser.Substitute(def.Title, vars).Trim();
            if (title.Length == 0) title = scope.EditionName ?? "Briefing";

            List<PlannedSection> plan = new List<PlannedSection>();
            HashSet<string> usedFindings = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedPredictions = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> usedSuggestions = new HashSet<string>(StringComparer.Ordinal);
            List<SourceItem> citations = new List<SourceItem>();
            foreach (TemplateSection s in def.Sections)
            {
                PlannedSection ps = new PlannedSection { Section = s };
                switch (s.Directive.Source)
                {
                    case "findings":
                        ps.Findings = SelectFindings(data, s.Directive, usedFindings);
                        foreach (FindingGroup g in ps.Findings) foreach (Finding f in g.Items) { usedFindings.Add(f.Id); Cite(data, f, citations); }
                        break;
                    case "predictions":
                        ps.Predictions = SelectPredictions(data, s.Directive, scope, usedPredictions);
                        foreach (Prediction p in ps.Predictions) usedPredictions.Add(p.Id);
                        break;
                    case "suggestions":
                        ps.Suggestions = SelectSuggestions(data, s.Directive, usedSuggestions);
                        foreach (Suggestion x in ps.Suggestions) usedSuggestions.Add(x.Id);
                        break;
                    case "run-summaries":
                        ps.Runs = data.Runs.Where(r => !String.IsNullOrWhiteSpace(r.Summary)).OrderByDescending(r => r.CompletedUtc ?? r.CreatedUtc).Take(s.Directive.Limit).ToList();
                        break;
                }

                plan.Add(ps);
            }

            int fallbacks = 0;
            using (SemaphoreSlim gate = new SemaphoreSlim(Math.Max(1, _Settings.Pipeline.MaxConcurrentModelCalls)))
            {
                await Task.WhenAll(plan.Where(p => p.Section.Directive.Source != "citations" && !p.IsEmpty).Select(async p =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        p.Body = await WriteSectionAsync(briefing, scope, def, data, p, citations, token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is ModelCallException || ex is StructuredOutputException)
                    {
                        Interlocked.Increment(ref fallbacks);
                        p.Body = Plain(p, data, citations, def);
                        _Logging.Warn("[BriefingComposer] section '" + p.Section.Heading + "' written without a model: " + ex.Message);
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);
            }

            foreach (PlannedSection p in plan.Where(p => p.Section.Directive.Source == "citations"))
                p.Body = def.Citations == "none" || citations.Count == 0 ? null : CitationList(citations);

            List<PlannedSection> written = plan.Where(p => !String.IsNullOrWhiteSpace(p.Body)).ToList();
            string opening;
            int itemCount = written.Sum(p => p.ItemCount);
            if (itemCount == 0)
            {
                opening = "Nothing new in this period across " + (scope.Topics.Count == 1 ? scope.Topics[0].Name : scope.Topics.Count + " topics") + ".";
            }
            else
            {
                try
                {
                    opening = await WriteOpeningAsync(briefing, scope, def, vars, written, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ModelCallException || ex is StructuredOutputException)
                {
                    fallbacks++;
                    Finding? top = written.SelectMany(p => p.Findings.SelectMany(g => g.Items)).OrderByDescending(f => f.RelevanceScore).FirstOrDefault();
                    opening = top == null ? itemCount + " items this period." : "The most important development: **" + top.Title + "**. " + (top.Summary ?? String.Empty);
                }
            }

            StringBuilder md = new StringBuilder();
            md.Append("# ").Append(title).Append("\n\n").Append(opening.Trim()).Append("\n\n");
            foreach (PlannedSection p in written) md.Append("## ").Append(p.Section.Heading).Append("\n\n").Append(p.Body!.Trim()).Append("\n\n");
            string markdown = md.ToString().TrimEnd() + "\n";

            briefing.Title = Truncate(title, 256);
            briefing.Markdown = markdown;
            briefing.PeriodStartUtc = scope.PeriodStartUtc;
            briefing.PeriodEndUtc = scope.PeriodEndUtc;
            briefing.BriefingTemplateVersionId = String.IsNullOrEmpty(scope.Template.Version.Id) ? null : scope.Template.Version.Id;
            briefing.ItemCount = itemCount;
            briefing.WordCount = CountWords(SafeMarkdown.ToText(markdown));
            briefing.ReadMinutes = Math.Max(1, (int)Math.Ceiling(briefing.WordCount / 220.0));
            briefing.ModelFallbacks = fallbacks;
            briefing.ComposedUtc = DateTime.UtcNow;
            briefing.Status = BriefingStatusEnum.Ready;
            briefing.ErrorMessage = null;
            if (!scope.Save) return briefing;

            int order = 0;
            BriefingSection opener = await _Database.BriefingSections.CreateAsync(new BriefingSection { TenantId = scope.TenantId, BriefingId = briefing.Id, SectionKey = "opening", Heading = title, Markdown = opening.Trim(), Source = "opening", SortOrder = order++ }, token).ConfigureAwait(false);
            foreach (PlannedSection p in written)
            {
                BriefingSection row = await _Database.BriefingSections.CreateAsync(new BriefingSection
                {
                    TenantId = scope.TenantId,
                    BriefingId = briefing.Id,
                    SectionKey = p.Section.Key,
                    Heading = Truncate(p.Section.Heading, 256),
                    Markdown = p.Body!.Trim(),
                    Source = p.Section.Directive.Source,
                    SortOrder = order++
                }, token).ConfigureAwait(false);
                int i = 0;
                foreach (Finding f in p.Findings.SelectMany(g => g.Items))
                    await AddItemAsync(scope, briefing, row, BriefingItemTypeEnum.Finding, f.Id, CitationNumber(f, citations), i++, token).ConfigureAwait(false);
                foreach (Prediction x in p.Predictions) await AddItemAsync(scope, briefing, row, BriefingItemTypeEnum.Prediction, x.Id, null, i++, token).ConfigureAwait(false);
                foreach (Suggestion x in p.Suggestions) await AddItemAsync(scope, briefing, row, BriefingItemTypeEnum.Suggestion, x.Id, null, i++, token).ConfigureAwait(false);
            }

            if (!scope.IsDryRun) await RenderFilesAsync(briefing, token).ConfigureAwait(false);
            Briefing saved = await _Database.Briefings.UpdateAsync(briefing, token).ConfigureAwait(false);
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.Briefings, "kind", briefing.Kind.ToString(), "outcome", fallbacks > 0 ? "fallback" : "ready");
            return saved;
        }

        /// <summary>
        /// Store the briefing's Markdown and rendered HTML as artifacts (replacing the links on the row, not saving it).
        /// Used after composition and to regenerate expired files.
        /// </summary>
        /// <param name="briefing">Briefing with Markdown.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RenderFilesAsync(Briefing briefing, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(briefing);
            string markdown = briefing.Markdown ?? String.Empty;
            string baseName = FileBase(briefing);
            string meta = (briefing.ComposedUtc ?? DateTime.UtcNow).ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) + " · " + briefing.ReadMinutes + " min read";
            Artifact md = await _Artifacts.SaveAsync(new ArtifactWrite
            {
                TenantId = briefing.TenantId, UserId = briefing.UserId, Kind = ArtifactKindEnum.BriefingMarkdown, TopicId = briefing.TopicId, BriefingId = briefing.Id,
                ResearchRequestId = briefing.ResearchRequestId, FileName = baseName + ".md", ContentType = "text/markdown; charset=utf-8", Extension = "md",
                Content = Encoding.UTF8.GetBytes(markdown), Pinned = briefing.Pinned
            }, token).ConfigureAwait(false);
            Artifact html = await _Artifacts.SaveAsync(new ArtifactWrite
            {
                TenantId = briefing.TenantId, UserId = briefing.UserId, Kind = ArtifactKindEnum.BriefingHtml, TopicId = briefing.TopicId, BriefingId = briefing.Id,
                ResearchRequestId = briefing.ResearchRequestId, FileName = baseName + ".html", ContentType = "text/html; charset=utf-8", Extension = "html",
                Content = Encoding.UTF8.GetBytes(BriefingHtmlRenderer.Render(briefing.Title, markdown, meta)), Pinned = briefing.Pinned
            }, token).ConfigureAwait(false);
            briefing.MarkdownArtifactId = md.Id;
            briefing.HtmlArtifactId = html.Id;
        }

        #endregion

        #region Private-Methods

        private async Task<ComposeData> LoadAsync(ComposeScope scope, CancellationToken token)
        {
            ComposeData data = new ComposeData();
            foreach (Topic t in scope.Topics)
            {
                data.TopicsById[t.Id] = t;
                List<Finding> findings = (await _Database.Findings.EnumerateAsync(new FindingFilter { TenantId = scope.TenantId, TopicId = t.Id, RunId = scope.RunId, IsDuplicate = false, Hidden = false, IsDryRun = scope.IsDryRun, PageSize = 500 }, token).ConfigureAwait(false)).Items;
                data.Findings.AddRange(findings.Where(f => f.Tier != FindingTierEnum.BelowThreshold && (scope.RunId != null || (f.CreatedUtc >= scope.PeriodStartUtc && f.CreatedUtc <= scope.PeriodEndUtc))));
                List<Prediction> preds = (await _Database.Predictions.EnumerateAsync(new PredictionFilter { TenantId = scope.TenantId, TopicId = t.Id, IsDryRun = scope.IsDryRun, PageSize = 500 }, token).ConfigureAwait(false)).Items;
                data.Predictions.AddRange(preds);
                List<Suggestion> sugs = (await _Database.Suggestions.EnumerateAsync(new SuggestionFilter { TenantId = scope.TenantId, TopicId = t.Id, IsDryRun = scope.IsDryRun, PageSize = 500 }, token).ConfigureAwait(false)).Items;
                data.Suggestions.AddRange(sugs.Where(s => scope.RunId == null || s.RunId == scope.RunId));
                foreach (TopicDimension d in (await _Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = scope.TenantId, TopicId = t.Id, PageSize = 200 }, token).ConfigureAwait(false)).Items)
                    data.Dimensions[d.Id] = d;
                List<Run> runs = (await _Database.Runs.EnumerateAsync(new RunFilter { TenantId = scope.TenantId, TopicId = t.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items;
                data.Runs.AddRange(runs.Where(r => (scope.RunId == null ? (r.CompletedUtc >= scope.PeriodStartUtc && r.CompletedUtc <= scope.PeriodEndUtc) : r.Id == scope.RunId)
                    && (r.Status == RunStatusEnum.Completed || r.Status == RunStatusEnum.PartiallySucceeded || r.Status == RunStatusEnum.Running)));
            }

            foreach (string sid in data.Findings.Select(f => f.SourceItemId ?? String.Empty).Where(s => s.Length > 0).Distinct())
            {
                SourceItem? s = await _Database.SourceItems.ReadAsync(scope.TenantId, sid, token).ConfigureAwait(false);
                if (s != null) data.Sources[s.Id] = s;
            }

            return data;
        }

        private List<FindingGroup> SelectFindings(ComposeData data, SectionDirective d, HashSet<string> used)
        {
            int min = d.Tier != null && _TierRank.TryGetValue(d.Tier, out int r) ? r : 1;
            IEnumerable<Finding> pool = data.Findings.Where(f => !used.Contains(f.Id) && Rank(f.Tier) >= min);
            if (d.Topic != null) pool = pool.Where(f => data.TopicsById.TryGetValue(f.TopicId, out Topic? t) && (t.Id == d.Topic || String.Equals(t.Slug, d.Topic, StringComparison.OrdinalIgnoreCase)));
            if (d.Dimension != null) pool = pool.Where(f => f.DimensionId != null && data.Dimensions.TryGetValue(f.DimensionId, out TopicDimension? dim) && String.Equals(dim.Name, d.Dimension, StringComparison.OrdinalIgnoreCase));
            pool = d.Order == "recency" ? pool.OrderByDescending(f => f.PublishedUtc ?? f.CreatedUtc) : pool.OrderByDescending(f => f.RelevanceScore).ThenBy(f => f.Rank);
            List<Finding> list = pool.ToList();
            if (d.Group == null) return list.Count == 0 ? new List<FindingGroup>() : new List<FindingGroup> { new FindingGroup { Name = null, Items = list.Take(d.Limit).ToList() } };
            Func<Finding, string> key = d.Group == "topic"
                ? f => data.TopicsById.TryGetValue(f.TopicId, out Topic? t) ? t.Name : "Other"
                : d.Group == "dimension"
                    ? f => f.DimensionId != null && data.Dimensions.TryGetValue(f.DimensionId, out TopicDimension? dim) ? dim.Name : "General"
                    : f => f.Category.ToString();
            return list.GroupBy(key).Select(g => new FindingGroup { Name = g.Key, Items = g.Take(d.Limit).ToList() }).ToList();
        }

        private static List<Prediction> SelectPredictions(ComposeData data, SectionDirective d, ComposeScope scope, HashSet<string> used)
        {
            string status = d.Status ?? "new";
            IEnumerable<Prediction> pool = data.Predictions.Where(p => !used.Contains(p.Id) && p.Status != PredictionStatusEnum.Withdrawn);
            switch (status)
            {
                case "due":
                    pool = pool.Where(p => p.Status == PredictionStatusEnum.ProposedResolved || (p.Status == PredictionStatusEnum.Open && p.HorizonUtc <= scope.PeriodEndUtc.AddDays(7)));
                    break;
                case "open":
                    pool = pool.Where(p => p.Status == PredictionStatusEnum.Open);
                    break;
                case "resolved":
                    pool = pool.Where(p => p.ResolvedUtc.HasValue && p.ResolvedUtc >= scope.PeriodStartUtc && p.ResolvedUtc <= scope.PeriodEndUtc);
                    break;
                default:
                    pool = pool.Where(p => p.Status == PredictionStatusEnum.Open && (scope.RunId != null ? p.RunId == scope.RunId : p.CreatedUtc >= scope.PeriodStartUtc && p.CreatedUtc <= scope.PeriodEndUtc));
                    break;
            }

            string order = d.Order ?? (status == "due" ? "horizon" : "confidence");
            pool = order == "horizon" ? pool.OrderBy(p => p.HorizonUtc) : order == "recency" ? pool.OrderByDescending(p => p.CreatedUtc) : pool.OrderByDescending(p => p.Confidence);
            return pool.Take(d.Limit).ToList();
        }

        private static List<Suggestion> SelectSuggestions(ComposeData data, SectionDirective d, HashSet<string> used)
        {
            SuggestionStatusEnum status = Enum.TryParse(d.Status ?? "New", true, out SuggestionStatusEnum st) ? st : SuggestionStatusEnum.New;
            DateTime now = DateTime.UtcNow;
            IEnumerable<Suggestion> pool = data.Suggestions.Where(s => !used.Contains(s.Id) &&
                (s.Status == status || (status == SuggestionStatusEnum.New && s.Status == SuggestionStatusEnum.Snoozed && s.SnoozedUntilUtc.HasValue && s.SnoozedUntilUtc <= now)));
            pool = d.Order == "recency" ? pool.OrderByDescending(s => s.CreatedUtc) : pool.OrderBy(s => (int)s.Priority).ThenByDescending(s => s.CreatedUtc);
            return pool.Take(d.Limit).ToList();
        }

        private async Task<string> WriteSectionAsync(Briefing briefing, ComposeScope scope, BriefingTemplateDefinition def, ComposeData data, PlannedSection p, List<SourceItem> citations, CancellationToken token)
        {
            List<string> items = ItemLines(p, data, citations, def);
            string instruction = p.Section.Instruction.Length > 0 ? TemplateParser.Substitute(p.Section.Instruction, Variables(scope)) : DefaultInstruction(p.Section.Directive.Source);
            if (p.Section.Directive.Source == "none")
                items = data.Findings.OrderByDescending(f => f.RelevanceScore).Take(10).Select(f => FindingLine(f, data, citations, def)).ToList();
            Dictionary<string, object?> values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "section.heading", p.Section.Heading },
                { "section.instruction", instruction + (p.Section.Directive.Group != null ? "\nItems are grouped; use a ### subheading for each group." : String.Empty) },
                { "items", items },
                { "template.tone", def.Tone },
                { "template.audience", def.Audience },
                { "template.length", def.Length },
                { "citations.style", CitationStyle(def.Citations) }
            };
            string text = await CallTextAsync(briefing, scope, "briefing.section", values, token).ConfigureAwait(false);
            return StripHeading(text, p.Section.Heading);
        }

        private async Task<string> WriteOpeningAsync(Briefing briefing, ComposeScope scope, BriefingTemplateDefinition def, Dictionary<string, string> vars, List<PlannedSection> written, CancellationToken token)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "edition.name", vars["edition.name"] },
                { "date.long", vars["date.long"] },
                { "template.tone", def.Tone },
                { "template.audience", def.Audience },
                { "reader.name", vars["reader.name"] },
                { "opening.instruction", def.OpeningInstruction.Length > 0 ? TemplateParser.Substitute(def.OpeningInstruction, vars) : "Tell the reader the single most important thing and why it matters." },
                { "sections", written.Where(w => w.Section.Directive.Source != "citations").Select(w => "## " + w.Section.Heading + "\n" + Truncate(w.Body ?? String.Empty, 1200)).ToList() }
            };
            return await CallTextAsync(briefing, scope, "briefing.compose", values, token).ConfigureAwait(false);
        }

        private async Task<string> CallTextAsync(Briefing briefing, ComposeScope scope, string key, Dictionary<string, object?> values, CancellationToken token)
        {
            ResolvedPrompt prompt = await _Prompts.ResolveAsync(scope.TenantId, key, scope.Topics.Count == 1 ? scope.Topics[0].Id : null, token).ConfigureAwait(false);
            RenderedPrompt rendered = PromptService.Render(prompt.Definition, values);
            ModelCallResult result = await _Models.ChatAsync(new ModelCallRequest
            {
                TenantId = scope.TenantId,
                UserId = scope.UserId,
                TopicId = scope.Topics.Count == 1 ? scope.Topics[0].Id : null,
                RunId = scope.RunId,
                BriefingId = scope.Save ? briefing.Id : null,
                Task = prompt.Definition.Role,
                PromptKey = key,
                PromptTemplateVersionId = prompt.Version.Id,
                SystemPrompt = rendered.System,
                UserPrompt = scope.HistoryInstructions + "\n\n" + rendered.User,
                IsDryRun = scope.IsDryRun || !scope.Save
            }, token).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(result.Text)) throw new ModelCallException("The model returned an empty section.");
            return result.Text.Trim();
        }

        private static List<string> ItemLines(PlannedSection p, ComposeData data, List<SourceItem> citations, BriefingTemplateDefinition def)
        {
            List<string> lines = new List<string>();
            foreach (FindingGroup g in p.Findings)
            {
                if (g.Name != null) lines.Add("Group: " + g.Name);
                foreach (Finding f in g.Items) lines.Add(FindingLine(f, data, citations, def));
            }

            foreach (Prediction x in p.Predictions)
                lines.Add("Prediction: " + x.Statement + " (confidence " + Pct(x.Confidence) + ", by " + x.HorizonUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) + ", status " + x.Status + ")" +
                    (String.IsNullOrWhiteSpace(x.Rationale) ? String.Empty : ". Rationale: " + x.Rationale));
            foreach (Suggestion x in p.Suggestions)
                lines.Add("Suggestion: " + x.Title + " (priority " + x.Priority + ", effort " + x.Effort + ")" + (String.IsNullOrWhiteSpace(x.Description) ? String.Empty : ": " + x.Description) +
                    (String.IsNullOrWhiteSpace(x.Rationale) ? String.Empty : " Why: " + x.Rationale));
            foreach (Run r in p.Runs)
                lines.Add((data.TopicsById.TryGetValue(r.TopicId, out Topic? t) ? t.Name : "Topic") + ", " + (r.CompletedUtc ?? r.CreatedUtc).ToString("MMM d", CultureInfo.InvariantCulture) + ": " + r.Summary);
            return lines;
        }

        private static string FindingLine(Finding f, ComposeData data, List<SourceItem> citations, BriefingTemplateDefinition def)
        {
            int? n = CitationNumber(f, citations);
            SourceItem? s = f.SourceItemId != null && data.Sources.TryGetValue(f.SourceItemId, out SourceItem? src) ? src : null;
            string topic = data.TopicsById.TryGetValue(f.TopicId, out Topic? t) ? t.Name : String.Empty;
            string cite = def.Citations == "numbered" && n.HasValue ? "[" + n.Value + "] " : String.Empty;
            string url = def.Citations == "inline" && s != null ? " Source: " + s.CanonicalUrl : (s?.Domain != null ? " Source: " + s.Domain : String.Empty);
            return cite + f.Title + " (" + (topic.Length > 0 ? topic + "; " : String.Empty) + f.Tier + ", score " + f.RelevanceScore.ToString("0", CultureInfo.InvariantCulture) + "): " +
                (f.Summary ?? String.Empty) + (String.IsNullOrWhiteSpace(f.WhyItMatters) ? String.Empty : " Why it matters: " + f.WhyItMatters) + url;
        }

        private static string Plain(PlannedSection p, ComposeData data, List<SourceItem> citations, BriefingTemplateDefinition def)
        {
            StringBuilder sb = new StringBuilder();
            foreach (FindingGroup g in p.Findings)
            {
                if (g.Name != null) sb.Append("### ").Append(g.Name).Append("\n\n");
                foreach (Finding f in g.Items)
                {
                    int? n = CitationNumber(f, citations);
                    SourceItem? s = f.SourceItemId != null && data.Sources.TryGetValue(f.SourceItemId, out SourceItem? src) ? src : null;
                    string cite = def.Citations == "numbered" && n.HasValue ? " [" + n.Value + "]" : def.Citations == "inline" && s != null ? " ([source](" + s.CanonicalUrl + "))" : String.Empty;
                    sb.Append("- **").Append(f.Title).Append("**").Append(cite).Append(String.IsNullOrWhiteSpace(f.Summary) ? String.Empty : ": " + f.Summary).Append('\n');
                }

                sb.Append('\n');
            }

            foreach (Prediction x in p.Predictions)
                sb.Append("- ").Append(x.Statement).Append(" (").Append(Pct(x.Confidence)).Append(" by ").Append(x.HorizonUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)).Append(")\n");
            foreach (Suggestion x in p.Suggestions)
                sb.Append("- **").Append(x.Title).Append("**").Append(String.IsNullOrWhiteSpace(x.Description) ? String.Empty : ": " + x.Description).Append('\n');
            foreach (Run r in p.Runs)
                sb.Append("- ").Append(data.TopicsById.TryGetValue(r.TopicId, out Topic? t) ? t.Name + ": " : String.Empty).Append(FirstLine(r.Summary)).Append('\n');
            if (p.Section.Directive.Source == "none") sb.Append(p.Section.Instruction);
            return sb.ToString().Trim();
        }

        private static string CitationList(List<SourceItem> citations)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < citations.Count; i++)
            {
                SourceItem s = citations[i];
                string title = String.IsNullOrWhiteSpace(s.Title) ? (s.Domain ?? s.CanonicalUrl) : s.Title!.Replace("[", "(").Replace("]", ")");
                sb.Append(i + 1).Append(". [").Append(title).Append("](").Append(s.CanonicalUrl).Append(')');
                if (!String.IsNullOrWhiteSpace(s.Domain)) sb.Append(", ").Append(s.Domain);
                if (s.PublishedUtc.HasValue) sb.Append(", ").Append(s.PublishedUtc.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static void Cite(ComposeData data, Finding f, List<SourceItem> citations)
        {
            if (f.SourceItemId == null || !data.Sources.TryGetValue(f.SourceItemId, out SourceItem? s)) return;
            if (!citations.Any(c => c.Id == s.Id)) citations.Add(s);
        }

        private static int? CitationNumber(Finding f, List<SourceItem> citations)
        {
            if (f.SourceItemId == null) return null;
            int i = citations.FindIndex(c => c.Id == f.SourceItemId);
            return i < 0 ? null : i + 1;
        }

        private async Task AddItemAsync(ComposeScope scope, Briefing briefing, BriefingSection row, BriefingItemTypeEnum type, string id, int? citation, int order, CancellationToken token)
        {
            await _Database.BriefingItems.CreateAsync(new BriefingItem { TenantId = scope.TenantId, BriefingId = briefing.Id, SectionId = row.Id, ItemType = type, ItemId = id, CitationNumber = citation, SortOrder = order }, token).ConfigureAwait(false);
        }

        private static Dictionary<string, string> Variables(ComposeScope scope)
        {
            DateTime local = ToLocal(scope.PeriodEndUtc, scope.TimeZone);
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "edition.name", scope.EditionName ?? (scope.Topics.Count == 1 ? scope.Topics[0].Name : "Briefing") },
                { "date.long", local.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture) },
                { "date.short", local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                { "topic.name", scope.Topics.Count == 1 ? scope.Topics[0].Name : String.Join(", ", scope.Topics.Select(t => t.Name)) },
                { "reader.name", String.IsNullOrWhiteSpace(scope.ReaderName) ? "the reader" : scope.ReaderName! },
                { "period.start", ToLocal(scope.PeriodStartUtc, scope.TimeZone).ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) },
                { "period.end", local.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) }
            };
        }

        private static DateTime ToLocal(DateTime utc, string timeZone)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(String.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone));
            }
            catch (TimeZoneNotFoundException)
            {
                return utc;
            }
            catch (InvalidTimeZoneException)
            {
                return utc;
            }
        }

        private static string DefaultInstruction(string source)
        {
            switch (source)
            {
                case "findings": return "Summarize each item in one or two sentences and say why it matters.";
                case "predictions": return "State each prediction with its confidence and timeframe.";
                case "suggestions": return "One line per suggestion: what to do and why.";
                case "run-summaries": return "Summarize what each run found.";
                default: return "Write this section from the material provided.";
            }
        }

        private static string CitationStyle(string citations)
        {
            switch (citations)
            {
                case "inline": return "cite each source with a Markdown link to the URL given";
                case "none": return "do not cite sources";
                default: return "cite as [n] after the claim it supports";
            }
        }

        private static string StripHeading(string text, string heading)
        {
            string t = text.Trim();
            if (t.StartsWith("## ", StringComparison.Ordinal) || t.StartsWith("# ", StringComparison.Ordinal))
            {
                int nl = t.IndexOf('\n');
                string first = (nl < 0 ? t : t.Substring(0, nl)).TrimStart('#').Trim();
                if (String.Equals(first, heading, StringComparison.OrdinalIgnoreCase)) t = nl < 0 ? String.Empty : t.Substring(nl + 1).Trim();
            }

            return t;
        }

        private static string FileBase(Briefing b)
        {
            string slug = TemplateParser.Slug(b.Title);
            if (slug.Length > 60) slug = slug.Substring(0, 60).Trim('-');
            return (b.ComposedUtc ?? DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-" + slug;
        }

        private static int Rank(FindingTierEnum tier)
        {
            return tier == FindingTierEnum.MustRead ? 3 : tier == FindingTierEnum.WorthALook ? 2 : tier == FindingTierEnum.Fyi ? 1 : 0;
        }

        private static string Pct(double confidence)
        {
            return Math.Round(Math.Clamp(confidence, 0, 1) * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static string FirstLine(string? text)
        {
            string t = (text ?? String.Empty).Trim();
            int nl = t.IndexOf('\n');
            return nl < 0 ? t : t.Substring(0, nl).Trim();
        }

        private static int CountWords(string text)
        {
            return text.Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        #endregion
    }
}
