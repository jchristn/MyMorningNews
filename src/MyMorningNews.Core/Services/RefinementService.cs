namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Prompts;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using SyslogLogging;

    /// <summary>
    /// The topic refinement wizard (plan section 13): a session refines a topic that exists from the start in Draft status,
    /// so dimensions, constraints, and the brief are ordinary topic rows. Every model call goes through the gateway with
    /// the session, topic, and prompt version recorded. Locked items are kept when a step is regenerated.
    /// Thread safety: safe for concurrent use; a single session should not be driven concurrently.
    /// </summary>
    public class RefinementService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Timeout for live checks of suggested sources, in milliseconds. Default 8000. Minimum 1000, maximum 60000.
        /// </summary>
        public int SourceCheckTimeoutMs
        {
            get { return _SourceCheckTimeoutMs; }
            set { _SourceCheckTimeoutMs = Math.Clamp(value, 1000, 60000); }
        }

        #endregion

        #region Private-Members

        private const int _MaxChildren = 500;
        private readonly DatabaseDriverBase _Database;
        private readonly PromptService _Prompts;
        private readonly ModelGateway _Models;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _Http;
        private readonly string _Header = "[Refinement] ";
        private int _SourceCheckTimeoutMs = 8000;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="prompts">Prompt service.</param>
        /// <param name="models">Model gateway.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public RefinementService(DatabaseDriverBase database, PromptService prompts, ModelGateway models, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _Http.DefaultRequestHeaders.UserAgent.ParseAdd("MyMorningNews/0.1 (+https://mymorningnews.ai)");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a session for a new topic. The topic is created in Draft status.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="request">Seed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Session state.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The seed text is empty or too long.</exception>
        public async Task<RefinementStateResponse> StartAsync(string tenantId, string userId, RefinementStartRequest request, CancellationToken token = default)
        {
            Require(tenantId, userId);
            ArgumentNullException.ThrowIfNull(request);
            string seed = (request.SeedText ?? String.Empty).Trim();
            if (seed.Length == 0) throw new ArgumentException("Describe what you want to stay on top of.", nameof(request));
            if (seed.Length > 4000) throw new ArgumentException("The description is limited to 4000 characters.", nameof(request));

            Topic topic = await CreateTopicAsync(tenantId, userId, NameFromSeed(seed), seed, false, token).ConfigureAwait(false);
            RefinementSession session = await _Database.RefinementSessions.CreateAsync(new RefinementSession
            {
                TenantId = tenantId,
                TopicId = topic.Id,
                UserId = userId,
                SeedText = seed,
                Goal = Clean(request.Goal, 2000),
                Depth = Clean(request.Depth, 64),
                HorizonText = Clean(request.Horizon, 128),
                Known = Clean(request.Known, 4000),
                ModelEndpointId = Clean(request.ModelEndpointId, 64)
            }, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.User, seed, null, token).ConfigureAwait(false);
            return await GetStateAsync(tenantId, session.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Start a session that refines an existing topic again, seeded from its current context.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="topic">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Session state.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<RefinementStateResponse> StartForTopicAsync(string tenantId, string userId, Topic topic, CancellationToken token = default)
        {
            Require(tenantId, userId);
            ArgumentNullException.ThrowIfNull(topic);
            string seed = topic.Name + (String.IsNullOrWhiteSpace(topic.Description) ? String.Empty : ": " + topic.Description);
            RefinementSession session = await _Database.RefinementSessions.CreateAsync(new RefinementSession
            {
                TenantId = tenantId,
                TopicId = topic.Id,
                UserId = userId,
                SeedText = seed,
                Known = Clean(topic.Brief, 4000),
                Step = RefinementStepEnum.Dimensions
            }, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.System, "Refining the existing topic \"" + topic.Name + "\".", null, token).ConfigureAwait(false);
            return await GetStateAsync(tenantId, session.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read the full state of a session.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>State.</returns>
        /// <exception cref="KeyNotFoundException">The session or its topic does not exist.</exception>
        public async Task<RefinementStateResponse> GetStateAsync(string tenantId, string sessionId, CancellationToken token = default)
        {
            RefinementSession session = await _Database.RefinementSessions.ReadAsync(tenantId, sessionId, token).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Refinement session " + sessionId + " was not found.");
            Topic topic = await _Database.Topics.ReadAsync(tenantId, session.TopicId, token).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Topic " + session.TopicId + " was not found.");
            RefinementStateResponse state = new RefinementStateResponse
            {
                Session = session,
                Topic = topic,
                Dimensions = await DimensionsAsync(tenantId, topic.Id, token).ConfigureAwait(false),
                Constraints = await ConstraintsAsync(tenantId, topic.Id, token).ConfigureAwait(false),
                Questions = await QuestionsAsync(tenantId, session.Id, null, token).ConfigureAwait(false),
                Messages = (await _Database.RefinementMessages.EnumerateAsync(new RefinementMessageFilter { TenantId = tenantId, SessionId = session.Id, PageSize = _MaxChildren }, token).ConfigureAwait(false)).Items
            };
            return state;
        }

        /// <summary>
        /// Propose dimensions (refinement.dimensions). Unlocked model-proposed dimensions are replaced; locked and
        /// reader-created dimensions are kept. When the model judges the request out of scope, nothing is replaced and the
        /// reason is stored on the session.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Calling user (attributed for cost).</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="guidance">Optional guidance.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated state.</returns>
        /// <exception cref="ModelCallException">The model call failed.</exception>
        /// <exception cref="ConflictException">The session is not active.</exception>
        public async Task<RefinementStateResponse> ProposeDimensionsAsync(string tenantId, string userId, string sessionId, string? guidance, CancellationToken token = default)
        {
            RefinementSession session = await ActiveSessionAsync(tenantId, sessionId, token).ConfigureAwait(false);
            List<TopicDimension> existing = await DimensionsAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            List<TopicDimension> kept = existing.Where(d => d.IsLocked || d.Origin == OriginEnum.User).ToList();
            Dictionary<string, object?> values = SeedValues(session);
            values["locked.dimensions"] = kept.Select(FormatDimension).ToList();
            values["guidance"] = Clean(guidance, 2000);

            StructuredCallResult<DimensionProposalResult> result = await CallAsync<DimensionProposalResult>(session, userId, "refinement.dimensions", values, token).ConfigureAwait(false);
            if (!String.IsNullOrWhiteSpace(result.Value.OutOfScopeReason))
            {
                session.OutOfScopeReason = result.Value.OutOfScopeReason.Trim();
                session.LastError = null;
                await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
                await AddMessageAsync(session, MessageRoleEnum.Assistant, session.OutOfScopeReason, result.LlmCallId, token).ConfigureAwait(false);
                return await GetStateAsync(tenantId, sessionId, token).ConfigureAwait(false);
            }

            foreach (TopicDimension d in existing.Where(d => !d.IsLocked && d.Origin == OriginEnum.Model))
                await _Database.TopicDimensions.DeleteAsync(tenantId, d.Id, token).ConfigureAwait(false);

            HashSet<string> keptNames = new HashSet<string>(kept.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
            int order = kept.Count == 0 ? 0 : kept.Max(d => d.SortOrder) + 1;
            int added = 0;
            foreach (ProposedDimension p in result.Value.Dimensions)
            {
                string name = Truncate(p.Name.Trim(), 200);
                if (!keptNames.Add(name)) continue;
                await _Database.TopicDimensions.CreateAsync(new TopicDimension
                {
                    TenantId = tenantId,
                    TopicId = session.TopicId,
                    Name = name,
                    Description = Clean(p.Description, 2000),
                    Rationale = Clean(p.Rationale, 2000),
                    Weight = p.Weight,
                    IncludeTerms = CleanList(p.IncludeTerms, 20),
                    ExcludeTerms = CleanList(p.ExcludeTerms, 20),
                    Origin = OriginEnum.Model,
                    SortOrder = order++
                }, token).ConfigureAwait(false);
                added++;
            }

            session.OutOfScopeReason = null;
            session.LastError = null;
            session.Step = RefinementStepEnum.Dimensions;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.Assistant, "Proposed " + added + " dimensions" + (kept.Count > 0 ? " and kept " + kept.Count + " of yours." : "."), result.LlmCallId, token).ConfigureAwait(false);
            return await GetStateAsync(tenantId, sessionId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Ask the next round of clarifying questions (refinement.questions for round 1, refinement.followup afterward).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Calling user.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="guidance">Optional guidance.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated state.</returns>
        /// <exception cref="ModelCallException">The model call failed.</exception>
        /// <exception cref="ConflictException">The session is not active or has no dimensions.</exception>
        public async Task<RefinementStateResponse> AskQuestionsAsync(string tenantId, string userId, string sessionId, string? guidance, CancellationToken token = default)
        {
            RefinementSession session = await ActiveSessionAsync(tenantId, sessionId, token).ConfigureAwait(false);
            List<TopicDimension> dimensions = await DimensionsAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            if (dimensions.Count == 0) throw new ConflictException("Propose or add dimensions before asking questions.");
            int round = session.Round + 1;
            Dictionary<string, object?> values = SeedValues(session);
            values["dimensions"] = dimensions.Select(FormatDimension).ToList();
            values["answers"] = await AnswersAsync(tenantId, session.Id, token).ConfigureAwait(false);
            values["round"] = round.ToString(CultureInfo.InvariantCulture);
            values["guidance"] = Clean(guidance, 2000);

            string key = round == 1 ? "refinement.questions" : "refinement.followup";
            StructuredCallResult<QuestionBatchResult> result = await CallAsync<QuestionBatchResult>(session, userId, key, values, token).ConfigureAwait(false);
            int order = 0;
            foreach (ProposedQuestion q in result.Value.Questions)
            {
                TopicDimension? dim = q.Dimension == null ? null : dimensions.FirstOrDefault(d => String.Equals(d.Name, q.Dimension.Trim(), StringComparison.OrdinalIgnoreCase));
                await CreateQuestionAsync(session, round, q, dim?.Id, order++, token).ConfigureAwait(false);
            }

            session.Round = round;
            session.SufficientContext = result.Value.SufficientContext;
            session.Step = RefinementStepEnum.Questions;
            session.LastError = null;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.Assistant, "Asked " + result.Value.Questions.Count + " questions in round " + round + (result.Value.SufficientContext ? "; that should be enough context." : "."), result.LlmCallId, token).ConfigureAwait(false);
            return await GetStateAsync(tenantId, sessionId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Answer or skip a question.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="questionId">Question.</param>
        /// <param name="answer">Answer.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The question with its options.</returns>
        /// <exception cref="KeyNotFoundException">The question does not belong to the session.</exception>
        /// <exception cref="ArgumentException">The answer does not fit the question kind.</exception>
        public async Task<RefinementQuestionResponse> AnswerAsync(string tenantId, string sessionId, string questionId, QuestionAnswerRequest answer, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(answer);
            RefinementQuestion question = await _Database.RefinementQuestions.ReadAsync(tenantId, questionId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Question " + questionId + " was not found.");
            if (!String.Equals(question.SessionId, sessionId, StringComparison.Ordinal)) throw new KeyNotFoundException("Question " + questionId + " was not found in this session.");
            List<RefinementQuestionOption> options = (await _Database.RefinementQuestionOptions.EnumerateAsync(new RefinementQuestionOptionFilter { TenantId = tenantId, QuestionId = questionId, PageSize = 100 }, token).ConfigureAwait(false)).Items;

            if (answer.Skipped)
            {
                question.Skipped = true;
                question.Answered = false;
                question.AnswerText = null;
                foreach (RefinementQuestionOption o in options.Where(o => o.Selected)) { o.Selected = false; await _Database.RefinementQuestionOptions.UpdateAsync(o, token).ConfigureAwait(false); }
            }
            else if (question.Kind == QuestionKindEnum.SingleChoice || question.Kind == QuestionKindEnum.MultiChoice)
            {
                HashSet<string> selected = new HashSet<string>(answer.SelectedOptionIds ?? new List<string>(), StringComparer.Ordinal);
                if (selected.Count == 0 && String.IsNullOrWhiteSpace(answer.AnswerText)) throw new ArgumentException("Pick an option or write an answer.", nameof(answer));
                if (selected.Any(id => !options.Any(o => o.Id == id))) throw new ArgumentException("Selected options must belong to the question.", nameof(answer));
                if (question.Kind == QuestionKindEnum.SingleChoice && selected.Count > 1) throw new ArgumentException("Pick one option.", nameof(answer));
                foreach (RefinementQuestionOption o in options)
                {
                    bool want = selected.Contains(o.Id);
                    if (o.Selected == want) continue;
                    o.Selected = want;
                    await _Database.RefinementQuestionOptions.UpdateAsync(o, token).ConfigureAwait(false);
                }

                question.AnswerText = Clean(answer.AnswerText, 4000);
                question.Answered = true;
                question.Skipped = false;
            }
            else
            {
                string text = (answer.AnswerText ?? String.Empty).Trim();
                if (text.Length == 0) throw new ArgumentException("Write an answer or skip the question.", nameof(answer));
                if (question.Kind == QuestionKindEnum.Scale && (!Int32.TryParse(text, out int scale) || scale < 1 || scale > 5)) throw new ArgumentException("Scale answers are 1 to 5.", nameof(answer));
                if (question.Kind == QuestionKindEnum.YesNo && !String.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) && !String.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Answer yes or no.", nameof(answer));
                question.AnswerText = Truncate(text, 4000);
                question.Answered = true;
                question.Skipped = false;
            }

            question = await _Database.RefinementQuestions.UpdateAsync(question, token).ConfigureAwait(false);
            return new RefinementQuestionResponse
            {
                Question = question,
                Options = (await _Database.RefinementQuestionOptions.EnumerateAsync(new RefinementQuestionOptionFilter { TenantId = tenantId, QuestionId = questionId, PageSize = 100 }, token).ConfigureAwait(false)).Items
            };
        }

        /// <summary>
        /// Draft the topic context (refinement.synthesize): brief, name, audience, horizon, novelty, seed queries, and
        /// constraints. Unlocked model-created constraints (other than feeds and seed URLs) are replaced.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Calling user.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated state.</returns>
        /// <exception cref="ModelCallException">The model call failed.</exception>
        public async Task<RefinementStateResponse> SynthesizeAsync(string tenantId, string userId, string sessionId, CancellationToken token = default)
        {
            RefinementSession session = await ActiveSessionAsync(tenantId, sessionId, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            List<TopicDimension> dimensions = await DimensionsAsync(tenantId, topic.Id, token).ConfigureAwait(false);
            List<TopicConstraint> existing = await ConstraintsAsync(tenantId, topic.Id, token).ConfigureAwait(false);
            List<TopicConstraint> kept = existing.Where(c => c.IsLocked || c.Origin == OriginEnum.User || c.Kind == ConstraintKindEnum.FeedUrl || c.Kind == ConstraintKindEnum.SeedUrl).ToList();

            Dictionary<string, object?> values = SeedValues(session);
            values["dimensions"] = dimensions.Select(FormatDimension).ToList();
            values["answers"] = await AnswersAsync(tenantId, session.Id, token).ConfigureAwait(false);
            values["locked.items"] = kept.Where(c => c.IsLocked).Select(c => c.Kind + ": " + c.Value).Concat(dimensions.Where(d => d.IsLocked).Select(d => "Dimension: " + d.Name)).ToList();

            StructuredCallResult<TopicContextResult> result = await CallAsync<TopicContextResult>(session, userId, "refinement.synthesize", values, token).ConfigureAwait(false);
            TopicContextResult ctx = result.Value;

            foreach (TopicConstraint c in existing.Except(kept))
                await _Database.TopicConstraints.DeleteAsync(tenantId, c.Id, token).ConfigureAwait(false);
            HashSet<string> seen = new HashSet<string>(kept.Select(c => c.Kind + "|" + c.Value), StringComparer.OrdinalIgnoreCase);
            await AddConstraintsAsync(topic, ConstraintKindEnum.IncludeKeyword, ctx.IncludeKeywords, seen, token).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.ExcludeKeyword, ctx.ExcludeKeywords, seen, token).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.IncludeEntity, ctx.IncludeEntities, seen, token).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.ExcludeEntity, ctx.ExcludeEntities, seen, token).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.PreferredDomain, ctx.PreferredDomains.Select(NormalizeDomain).ToList(), seen, token).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.BlockedDomain, ctx.BlockedDomains.Select(NormalizeDomain).ToList(), seen, token).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.TrackedQuestion, ctx.TrackedQuestions, seen, token).ConfigureAwait(false);

            if (!String.IsNullOrWhiteSpace(ctx.Name) && topic.Status == TopicStatusEnum.Draft) topic.Name = Truncate(ctx.Name.Trim(), 200);
            topic.Brief = ctx.Brief.Trim();
            topic.AudienceLevel = ctx.AudienceLevel;
            topic.Horizon = ctx.Horizon;
            topic.NoveltyPreference = ctx.NoveltyPreference;
            topic.SeedQueries = CleanList(ctx.SeedQueries, 12);
            await _Database.Topics.UpdateAsync(topic, token).ConfigureAwait(false);

            session.Step = RefinementStepEnum.Context;
            session.LastError = null;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.Assistant, "Drafted the brief with " + topic.SeedQueries.Count + " seed queries.", result.LlmCallId, token).ConfigureAwait(false);
            return await GetStateAsync(tenantId, sessionId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Suggest feeds and seed pages (refinement.sources) and check each with a live fetch. Nothing is saved until
        /// the reader accepts sources with <see cref="AcceptSourcesAsync"/>.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Calling user.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Suggestions with fetch results.</returns>
        /// <exception cref="ModelCallException">The model call failed.</exception>
        public async Task<List<SourceCheck>> SuggestSourcesAsync(string tenantId, string userId, string sessionId, CancellationToken token = default)
        {
            RefinementSession session = await ActiveSessionAsync(tenantId, sessionId, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            Dictionary<string, object?> values = new Dictionary<string, object?>
            {
                { "topic.name", topic.Name },
                { "topic.brief", topic.Brief ?? session.SeedText },
                { "dimensions", (await DimensionsAsync(tenantId, topic.Id, token).ConfigureAwait(false)).Select(FormatDimension).ToList() }
            };
            StructuredCallResult<SourceSuggestionResult> result = await CallAsync<SourceSuggestionResult>(session, userId, "refinement.sources", values, token).ConfigureAwait(false);
            List<SourceCheck> checks = result.Value.Feeds.Select(s => ToCheck(s, "FeedUrl")).Concat(result.Value.SeedUrls.Select(s => ToCheck(s, "SeedUrl"))).ToList();

            using (SemaphoreSlim gate = new SemaphoreSlim(4))
            {
                await Task.WhenAll(checks.Select(async c =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try { await CheckUrlAsync(c, token).ConfigureAwait(false); }
                    finally { gate.Release(); }
                })).ConfigureAwait(false);
            }

            session.Step = RefinementStepEnum.Sources;
            session.LastError = null;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.Assistant, "Suggested " + checks.Count + " sources; " + checks.Count(c => c.Reachable) + " responded.", result.LlmCallId, token).ConfigureAwait(false);
            return checks;
        }

        /// <summary>
        /// Save accepted feeds and seed pages as topic constraints.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="accept">URLs to add.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated state.</returns>
        /// <exception cref="ArgumentException">A URL is not an absolute http or https URL.</exception>
        public async Task<RefinementStateResponse> AcceptSourcesAsync(string tenantId, string sessionId, SourceAcceptRequest accept, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(accept);
            RefinementSession session = await ActiveSessionAsync(tenantId, sessionId, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            foreach (string url in (accept.FeedUrls ?? new List<string>()).Concat(accept.SeedUrls ?? new List<string>()))
                if (!IsHttpUrl(url)) throw new ArgumentException("Not an http or https URL: " + url, nameof(accept));
            HashSet<string> seen = new HashSet<string>((await ConstraintsAsync(tenantId, topic.Id, token).ConfigureAwait(false)).Select(c => c.Kind + "|" + c.Value), StringComparer.OrdinalIgnoreCase);
            await AddConstraintsAsync(topic, ConstraintKindEnum.FeedUrl, accept.FeedUrls ?? new List<string>(), seen, token, OriginEnum.User).ConfigureAwait(false);
            await AddConstraintsAsync(topic, ConstraintKindEnum.SeedUrl, accept.SeedUrls ?? new List<string>(), seen, token, OriginEnum.User).ConfigureAwait(false);
            return await GetStateAsync(tenantId, sessionId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Apply the session: the topic becomes Active and the session Completed.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated state.</returns>
        /// <exception cref="ConflictException">The topic has no brief or no dimensions yet.</exception>
        public async Task<RefinementStateResponse> CompleteAsync(string tenantId, string sessionId, CancellationToken token = default)
        {
            RefinementSession session = await ActiveSessionAsync(tenantId, sessionId, token).ConfigureAwait(false);
            Topic topic = await TopicAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(topic.Brief)) throw new ConflictException("Draft the context before applying the topic.");
            if ((await DimensionsAsync(tenantId, topic.Id, token).ConfigureAwait(false)).Count == 0) throw new ConflictException("A topic needs at least one dimension.");
            if (topic.Status == TopicStatusEnum.Draft) topic.Status = TopicStatusEnum.Active;
            topic.LastRefinedUtc = DateTime.UtcNow;
            await _Database.Topics.UpdateAsync(topic, token).ConfigureAwait(false);
            session.Status = RefinementStatusEnum.Completed;
            session.Step = RefinementStepEnum.Review;
            session.CompletedUtc = DateTime.UtcNow;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            await AddMessageAsync(session, MessageRoleEnum.System, "Topic applied.", null, token).ConfigureAwait(false);
            return await GetStateAsync(tenantId, sessionId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Abandon a session. A Draft topic that was created for it is deleted.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the session was abandoned.</returns>
        public async Task<bool> AbandonAsync(string tenantId, string sessionId, CancellationToken token = default)
        {
            RefinementSession? session = await _Database.RefinementSessions.ReadAsync(tenantId, sessionId, token).ConfigureAwait(false);
            if (session == null || session.Status != RefinementStatusEnum.Active) return false;
            Topic? topic = await _Database.Topics.ReadAsync(tenantId, session.TopicId, token).ConfigureAwait(false);
            if (topic != null && topic.Status == TopicStatusEnum.Draft && !topic.IsOneTime)
            {
                await _Database.Topics.DeleteAsync(tenantId, topic.Id, token).ConfigureAwait(false);
                return true;
            }

            session.Status = RefinementStatusEnum.Abandoned;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Create a topic with a unique slug.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="oneTime">Whether the topic backs a one-time request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Topic in Draft status.</returns>
        public async Task<Topic> CreateTopicAsync(string tenantId, string userId, string name, string? description, bool oneTime, CancellationToken token = default)
        {
            Require(tenantId, userId);
            string clean = Truncate(String.IsNullOrWhiteSpace(name) ? "Untitled topic" : name.Trim(), 200);
            return await _Database.Topics.CreateAsync(new Topic
            {
                TenantId = tenantId,
                UserId = userId,
                Name = clean,
                Slug = await UniqueSlugAsync(tenantId, clean, token).ConfigureAwait(false),
                Description = Clean(description, 4000),
                IsOneTime = oneTime,
                Status = TopicStatusEnum.Draft
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// A slug for a name that is unique in the tenant.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="name">Name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Slug.</returns>
        public async Task<string> UniqueSlugAsync(string tenantId, string name, CancellationToken token = default)
        {
            string baseSlug = Slugify(name);
            string slug = baseSlug;
            for (int i = 2; i < 1000; i++)
            {
                long count = await _Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, Slug = slug }, token).ConfigureAwait(false);
                if (count == 0) return slug;
                slug = Truncate(baseSlug, 190) + "-" + i.ToString(CultureInfo.InvariantCulture);
            }

            return Truncate(baseSlug, 180) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Resolve a prompt, render it, and make a structured model call attributed to the session, topic, and user.
        /// Failures are recorded on the session (LastError) and rethrown.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="session">Session.</param>
        /// <param name="userId">User for cost attribution.</param>
        /// <param name="key">Prompt key.</param>
        /// <param name="values">Prompt variables (dotted names).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Structured result.</returns>
        /// <exception cref="ModelCallException">The model call failed.</exception>
        /// <exception cref="ModelRefusalException">The model declined.</exception>
        /// <exception cref="StructuredOutputException">The output never validated.</exception>
        public async Task<StructuredCallResult<T>> CallAsync<T>(RefinementSession session, string? userId, string key, Dictionary<string, object?> values, CancellationToken token = default) where T : class, IStructuredResult
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(values);
            values["date.today"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ResolvedPrompt prompt = await _Prompts.ResolveAsync(session.TenantId, key, session.TopicId, token).ConfigureAwait(false);
            RenderedPrompt rendered = PromptService.Render(prompt.Definition, values);
            try
            {
                return await _Models.ChatStructuredAsync<T>(new ModelCallRequest
                {
                    TenantId = session.TenantId,
                    UserId = userId ?? session.UserId,
                    TopicId = session.TopicId,
                    RefinementSessionId = session.Id,
                    Task = prompt.Definition.Role,
                    PromptKey = key,
                    PromptTemplateVersionId = prompt.Version.Id,
                    ModelEndpointId = session.ModelEndpointId,
                    SystemPrompt = rendered.System,
                    UserPrompt = rendered.User
                }, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ModelCallException || ex is ModelRefusalException || ex is StructuredOutputException)
            {
                session.LastError = ex.Message;
                await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
                _Logging.Warn(_Header + key + " failed for session " + session.Id + ": " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Create a question and its options from a model proposal.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="round">Round.</param>
        /// <param name="proposal">Proposal.</param>
        /// <param name="dimensionId">Dimension, when any.</param>
        /// <param name="order">Sort order.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Question.</returns>
        public async Task<RefinementQuestion> CreateQuestionAsync(RefinementSession session, int round, ProposedQuestion proposal, string? dimensionId, int order, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(proposal);
            RefinementQuestion question = await _Database.RefinementQuestions.CreateAsync(new RefinementQuestion
            {
                TenantId = session.TenantId,
                SessionId = session.Id,
                Round = round,
                DimensionId = dimensionId,
                QuestionText = proposal.Question.Trim(),
                Rationale = Clean(proposal.Rationale, 1000),
                Kind = proposal.Kind,
                SortOrder = order
            }, token).ConfigureAwait(false);
            if (proposal.Kind == QuestionKindEnum.SingleChoice || proposal.Kind == QuestionKindEnum.MultiChoice)
            {
                int i = 0;
                foreach (string label in CleanList(proposal.Options, 12))
                {
                    await _Database.RefinementQuestionOptions.CreateAsync(new RefinementQuestionOption
                    {
                        TenantId = session.TenantId,
                        QuestionId = question.Id,
                        Label = label,
                        Value = Truncate(label, 200),
                        SortOrder = i++
                    }, token).ConfigureAwait(false);
                }
            }

            return question;
        }

        /// <summary>
        /// Questions with options for a session, optionally one round.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="round">Round, or null for all.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Questions in round and display order.</returns>
        public async Task<List<RefinementQuestionResponse>> QuestionsAsync(string tenantId, string sessionId, int? round, CancellationToken token = default)
        {
            List<RefinementQuestion> questions = (await _Database.RefinementQuestions.EnumerateAsync(new RefinementQuestionFilter { TenantId = tenantId, SessionId = sessionId, Round = round, PageSize = _MaxChildren }, token).ConfigureAwait(false)).Items
                .OrderBy(q => q.Round).ThenBy(q => q.SortOrder).ToList();
            List<RefinementQuestionResponse> result = new List<RefinementQuestionResponse>();
            foreach (RefinementQuestion q in questions)
            {
                List<RefinementQuestionOption> options = (await _Database.RefinementQuestionOptions.EnumerateAsync(new RefinementQuestionOptionFilter { TenantId = tenantId, QuestionId = q.Id, PageSize = 100 }, token).ConfigureAwait(false)).Items
                    .OrderBy(o => o.SortOrder).ToList();
                result.Add(new RefinementQuestionResponse { Question = q, Options = options });
            }

            return result;
        }

        /// <summary>
        /// Answered and skipped questions formatted for a prompt ("Q: ... A: ...").
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="sessionId">Session.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One line per question.</returns>
        public async Task<List<string>> AnswersAsync(string tenantId, string sessionId, CancellationToken token = default)
        {
            List<string> lines = new List<string>();
            foreach (RefinementQuestionResponse q in await QuestionsAsync(tenantId, sessionId, null, token).ConfigureAwait(false))
            {
                if (q.Question.Skipped) { lines.Add("Q: " + q.Question.QuestionText + " A: (skipped)"); continue; }
                if (!q.Question.Answered) continue;
                List<string> parts = q.Options.Where(o => o.Selected).Select(o => o.Label).ToList();
                if (!String.IsNullOrWhiteSpace(q.Question.AnswerText)) parts.Add(q.Question.AnswerText!);
                lines.Add("Q: " + q.Question.QuestionText + " A: " + String.Join("; ", parts));
            }

            return lines;
        }

        /// <summary>
        /// A dimension formatted for a prompt.
        /// </summary>
        /// <param name="d">Dimension.</param>
        /// <returns>One line.</returns>
        public static string FormatDimension(TopicDimension d)
        {
            ArgumentNullException.ThrowIfNull(d);
            StringBuilder sb = new StringBuilder(d.Name).Append(" (").Append(d.Weight).Append(')');
            if (!String.IsNullOrWhiteSpace(d.Description)) sb.Append(": ").Append(d.Description);
            if (d.IncludeTerms.Any()) sb.Append(" Include: ").Append(String.Join(", ", d.IncludeTerms)).Append('.');
            if (d.ExcludeTerms.Any()) sb.Append(" Exclude: ").Append(String.Join(", ", d.ExcludeTerms)).Append('.');
            return sb.ToString();
        }

        /// <summary>
        /// Dimensions of a topic in display order.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dimensions.</returns>
        public async Task<List<TopicDimension>> DimensionsAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            return (await _Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = tenantId, TopicId = topicId, PageSize = _MaxChildren }, token).ConfigureAwait(false)).Items
                .OrderBy(d => d.SortOrder).ToList();
        }

        /// <summary>
        /// Constraints of a topic.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Constraints.</returns>
        public async Task<List<TopicConstraint>> ConstraintsAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            return (await _Database.TopicConstraints.EnumerateAsync(new TopicConstraintFilter { TenantId = tenantId, TopicId = topicId, PageSize = _MaxChildren }, token).ConfigureAwait(false)).Items;
        }

        /// <summary>
        /// Add constraints of one kind, skipping blanks and duplicates.
        /// </summary>
        /// <param name="topic">Topic.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="values">Values.</param>
        /// <param name="seen">Kind and value pairs already present; updated.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="origin">Origin. Default Model.</param>
        /// <returns>Task.</returns>
        public async Task AddConstraintsAsync(Topic topic, ConstraintKindEnum kind, IEnumerable<string> values, HashSet<string> seen, CancellationToken token, OriginEnum origin = OriginEnum.Model)
        {
            ArgumentNullException.ThrowIfNull(topic);
            ArgumentNullException.ThrowIfNull(seen);
            foreach (string value in CleanList(values?.ToList() ?? new List<string>(), 30))
            {
                if (!seen.Add(kind + "|" + value)) continue;
                await _Database.TopicConstraints.CreateAsync(new TopicConstraint
                {
                    TenantId = topic.TenantId,
                    TopicId = topic.Id,
                    Kind = kind,
                    Value = value,
                    Origin = origin
                }, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Http.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private static void Require(string tenantId, string userId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
        }

        private async Task<RefinementSession> ActiveSessionAsync(string tenantId, string sessionId, CancellationToken token)
        {
            RefinementSession session = await _Database.RefinementSessions.ReadAsync(tenantId, sessionId, token).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Refinement session " + sessionId + " was not found.");
            if (session.Status != RefinementStatusEnum.Active) throw new ConflictException("This refinement session is " + session.Status.ToString().ToLowerInvariant() + ".");
            return session;
        }

        private async Task<Topic> TopicAsync(string tenantId, string topicId, CancellationToken token)
        {
            return await _Database.Topics.ReadAsync(tenantId, topicId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Topic " + topicId + " was not found.");
        }

        private async Task AddMessageAsync(RefinementSession session, MessageRoleEnum role, string content, string? llmCallId, CancellationToken token)
        {
            long count = await _Database.RefinementMessages.CountAsync(new RefinementMessageFilter { TenantId = session.TenantId, SessionId = session.Id }, token).ConfigureAwait(false);
            await _Database.RefinementMessages.CreateAsync(new RefinementMessage
            {
                TenantId = session.TenantId,
                SessionId = session.Id,
                Role = role,
                Content = content,
                LlmCallId = llmCallId,
                Sequence = (int)count
            }, token).ConfigureAwait(false);
        }

        private static Dictionary<string, object?> SeedValues(RefinementSession session)
        {
            return new Dictionary<string, object?>
            {
                { "seed.text", session.SeedText },
                { "seed.goal", session.Goal },
                { "seed.depth", session.Depth },
                { "seed.horizon", session.HorizonText },
                { "seed.known", session.Known }
            };
        }

        private static SourceCheck ToCheck(SuggestedSource s, string kind)
        {
            return new SourceCheck { Url = s.Url.Trim(), Kind = kind, Reason = s.Reason ?? String.Empty, Dimension = s.Dimension };
        }

        private async Task CheckUrlAsync(SourceCheck check, CancellationToken token)
        {
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, check.Url))
            {
                timeout.CancelAfter(_SourceCheckTimeoutMs);
                try
                {
                    using (HttpResponseMessage response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        check.StatusCode = (int)response.StatusCode;
                        check.ContentType = response.Content.Headers.ContentType?.MediaType;
                        check.Reachable = response.IsSuccessStatusCode;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is InvalidOperationException)
                {
                    if (token.IsCancellationRequested) throw;
                    check.Reachable = false;
                    check.Error = ex is OperationCanceledException ? "Timed out." : ex.Message;
                }
            }
        }

        private static bool IsHttpUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
        }

        private static string NormalizeDomain(string value)
        {
            string v = (value ?? String.Empty).Trim().ToLowerInvariant();
            if (Uri.TryCreate(v, UriKind.Absolute, out Uri? u)) v = u.Host;
            if (v.StartsWith("www.", StringComparison.Ordinal)) v = v.Substring(4);
            return v.TrimEnd('/');
        }

        private static string NameFromSeed(string seed)
        {
            string s = seed.Trim();
            foreach (string prefix in new[] { "i want to learn about ", "i want to keep up with ", "i want to follow ", "keep me updated on ", "tell me about " })
                if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(prefix.Length); break; }
            s = s.TrimEnd('.', '!', '?');
            if (s.Length > 80) s = s.Substring(0, 80).TrimEnd() + "...";
            return s.Length == 0 ? "Untitled topic" : Char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string Slugify(string name)
        {
            StringBuilder sb = new StringBuilder();
            bool dash = false;
            foreach (char c in name.ToLowerInvariant())
            {
                if (Char.IsLetterOrDigit(c) && c < 128) { sb.Append(c); dash = false; }
                else if (!dash && sb.Length > 0) { sb.Append('-'); dash = true; }
            }

            string slug = sb.ToString().Trim('-');
            if (slug.Length > 120) slug = slug.Substring(0, 120).Trim('-');
            return slug.Length == 0 ? "topic" : slug;
        }

        private static string? Clean(string? value, int max)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            return Truncate(value.Trim(), max);
        }

        private static List<string> CleanList(List<string>? values, int max)
        {
            if (values == null) return new List<string>();
            return values.Where(v => !String.IsNullOrWhiteSpace(v)).Select(v => Truncate(v.Trim(), 500)).Distinct(StringComparer.OrdinalIgnoreCase).Take(max).ToList();
        }

        private static string Truncate(string value, int max)
        {
            return value.Length > max ? value.Substring(0, max) : value;
        }

        #endregion
    }
}
