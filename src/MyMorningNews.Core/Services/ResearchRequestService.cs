namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// One-time requests (plan section 13.1): "build me a podcast on X" is planned with refinement.quick into a one-time
    /// topic (brief, dimensions, keywords, seed queries) plus at most three clarifying questions, then confirmed to run once,
    /// now or at a set time. The run itself is picked up by the scheduler and pipeline (Phases 5 and 6) through the same
    /// code paths as recurring topics.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class ResearchRequestService
    {
        #region Public-Members

        /// <summary>
        /// Allowed deliverables.
        /// </summary>
        public static readonly IReadOnlyList<string> Deliverables = new List<string> { "Report", "Podcast", "Email" };

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly RefinementService _Refinement;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="refinement">Refinement service (prompt calls, topics, questions).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ResearchRequestService(DatabaseDriverBase database, RefinementService refinement)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Refinement = refinement ?? throw new ArgumentNullException(nameof(refinement));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create and plan a request.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="user">Requesting user.</param>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The request with its plan and questions. Status Declined when the model judged it out of scope.</returns>
        /// <exception cref="ArgumentException">The request is invalid.</exception>
        /// <exception cref="ModelCallException">Planning failed.</exception>
        public async Task<ResearchRequestResponse> CreateAsync(string tenantId, User user, ResearchRequestCreateRequest request, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(request);
            string text = (request.RequestText ?? String.Empty).Trim();
            if (text.Length == 0) throw new ArgumentException("Say what you want researched.", nameof(request));
            if (text.Length > 4000) throw new ArgumentException("Requests are limited to 4000 characters.", nameof(request));
            List<string> deliverables = NormalizeDeliverables(request.Deliverables);
            if (request.RunAtUtc.HasValue && request.RunAtUtc.Value.ToUniversalTime() > DateTime.UtcNow.AddDays(366)) throw new ArgumentException("Requests can be scheduled up to a year ahead.", nameof(request));
            List<string> recipients = (request.EmailRecipients ?? new List<string>()).Where(e => !String.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
            if (recipients.Any(e => !e.Contains('@', StringComparison.Ordinal) || e.Length > 256)) throw new ArgumentException("Email recipients must be email addresses.", nameof(request));
            if (deliverables.Contains("Email") && recipients.Count == 0) recipients.Add(user.Email);

            Topic topic = await _Refinement.CreateTopicAsync(tenantId, user.Id, Shorten(text, 80), text, true, token).ConfigureAwait(false);
            RefinementSession session = await _Database.RefinementSessions.CreateAsync(new RefinementSession
            {
                TenantId = tenantId,
                TopicId = topic.Id,
                UserId = user.Id,
                SeedText = text,
                ModelEndpointId = String.IsNullOrWhiteSpace(request.ModelEndpointId) ? null : request.ModelEndpointId.Trim()
            }, token).ConfigureAwait(false);
            ResearchRequest rr = await _Database.ResearchRequests.CreateAsync(new ResearchRequest
            {
                TenantId = tenantId,
                UserId = user.Id,
                TopicId = topic.Id,
                RequestText = text,
                Title = Shorten(text, 80),
                Deliverables = deliverables,
                Length = request.Length,
                RunAtUtc = request.RunAtUtc.HasValue ? DateTime.SpecifyKind(request.RunAtUtc.Value.ToUniversalTime(), DateTimeKind.Utc) : null,
                EmailRecipients = recipients,
                PublishToFeed = request.PublishToFeed && deliverables.Contains("Podcast"),
                Status = ResearchRequestStatusEnum.Clarifying
            }, token).ConfigureAwait(false);

            await PlanAsync(rr, topic, session, request.SkipQuestions, token).ConfigureAwait(false);
            return await GetAsync(tenantId, rr.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Re-plan with the answers given so far (refinement.quick again). New questions are not asked again.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated request.</returns>
        /// <exception cref="ConflictException">The request is not waiting for clarification.</exception>
        public async Task<ResearchRequestResponse> ReplanAsync(string tenantId, string requestId, CancellationToken token = default)
        {
            ResearchRequest rr = await ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            if (rr.Status != ResearchRequestStatusEnum.Clarifying) throw new ConflictException("Only requests waiting for confirmation can be planned again.");
            Topic topic = await _Database.Topics.ReadAsync(tenantId, rr.TopicId!, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("The request topic was not found.");
            RefinementSession session = await SessionAsync(tenantId, rr, token).ConfigureAwait(false);
            await PlanAsync(rr, topic, session, true, token).ConfigureAwait(false);
            return await GetAsync(tenantId, rr.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Answer a clarifying question.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="questionId">Question.</param>
        /// <param name="answer">Answer.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The question.</returns>
        public async Task<RefinementQuestionResponse> AnswerAsync(string tenantId, string requestId, string questionId, QuestionAnswerRequest answer, CancellationToken token = default)
        {
            ResearchRequest rr = await ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            RefinementSession session = await SessionAsync(tenantId, rr, token).ConfigureAwait(false);
            return await _Refinement.AnswerAsync(tenantId, session.Id, questionId, answer, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Confirm a request so it runs once, now or at a set time.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="runAtUtc">Run time (UTC); null keeps the requested time or runs as soon as possible.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated request.</returns>
        /// <exception cref="ConflictException">The request cannot be confirmed in its current status.</exception>
        public async Task<ResearchRequestResponse> ConfirmAsync(string tenantId, string requestId, DateTime? runAtUtc, CancellationToken token = default)
        {
            ResearchRequest rr = await ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            if (rr.Status != ResearchRequestStatusEnum.Clarifying) throw new ConflictException("This request is " + rr.Status.ToString().ToLowerInvariant() + " and cannot be confirmed.");
            Topic topic = await _Database.Topics.ReadAsync(tenantId, rr.TopicId!, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("The request topic was not found.");
            if (String.IsNullOrWhiteSpace(topic.Brief)) throw new ConflictException("The request has no plan yet. Plan it again before confirming.");
            DateTime now = DateTime.UtcNow;
            DateTime when = runAtUtc.HasValue ? DateTime.SpecifyKind(runAtUtc.Value.ToUniversalTime(), DateTimeKind.Utc) : rr.RunAtUtc ?? now;
            if (when < now) when = now;
            rr.RunAtUtc = when;
            rr.Status = ResearchRequestStatusEnum.Scheduled;
            rr.ConfirmedUtc = now;
            await _Database.ResearchRequests.UpdateAsync(rr, token).ConfigureAwait(false);
            topic.Status = TopicStatusEnum.Active;
            await _Database.Topics.UpdateAsync(topic, token).ConfigureAwait(false);
            RefinementSession session = await SessionAsync(tenantId, rr, token).ConfigureAwait(false);
            session.Status = RefinementStatusEnum.Completed;
            session.CompletedUtc = now;
            await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            return await GetAsync(tenantId, rr.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Cancel a request that has not started.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated request.</returns>
        /// <exception cref="ConflictException">The request already ran or is running.</exception>
        public async Task<ResearchRequestResponse> CancelAsync(string tenantId, string requestId, CancellationToken token = default)
        {
            ResearchRequest rr = await ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            if (rr.Status != ResearchRequestStatusEnum.Clarifying && rr.Status != ResearchRequestStatusEnum.Scheduled)
                throw new ConflictException("Only requests that have not started can be cancelled.");
            rr.Status = ResearchRequestStatusEnum.Cancelled;
            await _Database.ResearchRequests.UpdateAsync(rr, token).ConfigureAwait(false);
            return await GetAsync(tenantId, rr.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Turn the request into a recurring topic with the same brief, dimensions, and constraints. A schedule is added
        /// to the topic separately (plan section 14).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The recurring topic.</returns>
        /// <exception cref="ConflictException">The request was declined or already converted.</exception>
        public async Task<Topic> ConvertAsync(string tenantId, string requestId, CancellationToken token = default)
        {
            ResearchRequest rr = await ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            if (rr.Status == ResearchRequestStatusEnum.Declined) throw new ConflictException("A declined request cannot become a topic.");
            if (!String.IsNullOrEmpty(rr.ConvertedTopicId)) throw new ConflictException("This request is already a recurring topic.");
            Topic topic = await _Database.Topics.ReadAsync(tenantId, rr.TopicId!, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("The request topic was not found.");
            topic.IsOneTime = false;
            topic.Status = TopicStatusEnum.Active;
            topic.LastRefinedUtc = DateTime.UtcNow;
            topic = await _Database.Topics.UpdateAsync(topic, token).ConfigureAwait(false);
            rr.ConvertedTopicId = topic.Id;
            await _Database.ResearchRequests.UpdateAsync(rr, token).ConfigureAwait(false);
            return topic;
        }

        /// <summary>
        /// Delete a request and, unless it became a recurring topic, its one-time topic.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when deleted.</returns>
        public async Task<bool> DeleteAsync(string tenantId, string requestId, CancellationToken token = default)
        {
            ResearchRequest? rr = await _Database.ResearchRequests.ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            if (rr == null) return false;
            await _Database.ResearchRequests.DeleteAsync(tenantId, rr.Id, token).ConfigureAwait(false);
            if (!String.IsNullOrEmpty(rr.TopicId) && String.IsNullOrEmpty(rr.ConvertedTopicId))
                await _Database.Topics.DeleteCascadeAsync(tenantId, rr.TopicId, token).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Read a request with its plan and questions.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="requestId">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response.</returns>
        /// <exception cref="KeyNotFoundException">Not found.</exception>
        public async Task<ResearchRequestResponse> GetAsync(string tenantId, string requestId, CancellationToken token = default)
        {
            ResearchRequest rr = await ReadAsync(tenantId, requestId, token).ConfigureAwait(false);
            ResearchRequestResponse response = new ResearchRequestResponse { Request = rr };
            if (!String.IsNullOrEmpty(rr.TopicId))
            {
                response.Topic = await _Database.Topics.ReadAsync(tenantId, rr.TopicId, token).ConfigureAwait(false);
                if (response.Topic != null)
                {
                    response.Dimensions = await _Refinement.DimensionsAsync(tenantId, response.Topic.Id, token).ConfigureAwait(false);
                    RefinementSession? session = (await _Database.RefinementSessions.EnumerateAsync(new RefinementSessionFilter { TenantId = tenantId, TopicId = response.Topic.Id, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
                    if (session != null)
                    {
                        response.RefinementSessionId = session.Id;
                        response.Questions = await _Refinement.QuestionsAsync(tenantId, session.Id, null, token).ConfigureAwait(false);
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// Normalize deliverable names (case-insensitive, de-duplicated, Report when none).
        /// </summary>
        /// <param name="values">Values.</param>
        /// <returns>Deliverables.</returns>
        /// <exception cref="ArgumentException">An unknown deliverable was given.</exception>
        public static List<string> NormalizeDeliverables(List<string>? values)
        {
            List<string> result = new List<string>();
            foreach (string v in values ?? new List<string>())
            {
                if (String.IsNullOrWhiteSpace(v)) continue;
                string? match = Deliverables.FirstOrDefault(d => String.Equals(d, v.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null) throw new ArgumentException("Unknown deliverable \"" + v + "\". Use Report, Podcast, or Email.", nameof(values));
                if (!result.Contains(match)) result.Add(match);
            }

            if (result.Count == 0) result.Add("Report");
            return result;
        }

        #endregion

        #region Private-Methods

        private async Task PlanAsync(ResearchRequest rr, Topic topic, RefinementSession session, bool skipQuestions, CancellationToken token)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>
            {
                { "request.text", rr.RequestText },
                { "request.deliverables", String.Join(", ", rr.Deliverables) },
                { "request.length", rr.Length.ToString() },
                { "answers", await _Refinement.AnswersAsync(rr.TenantId, session.Id, token).ConfigureAwait(false) }
            };
            StructuredCallResult<QuickRefineResult> result;
            try
            {
                result = await _Refinement.CallAsync<QuickRefineResult>(session, rr.UserId, "refinement.quick", values, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ModelCallException || ex is ModelRefusalException || ex is StructuredOutputException)
            {
                rr.ErrorMessage = ex.Message;
                await _Database.ResearchRequests.UpdateAsync(rr, token).ConfigureAwait(false);
                throw;
            }

            QuickRefineResult plan = result.Value;
            if (!String.IsNullOrWhiteSpace(plan.OutOfScopeReason))
            {
                rr.Status = ResearchRequestStatusEnum.Declined;
                rr.OutOfScopeReason = plan.OutOfScopeReason.Trim();
                rr.ErrorMessage = null;
                await _Database.ResearchRequests.UpdateAsync(rr, token).ConfigureAwait(false);
                return;
            }

            rr.Title = Shorten(plan.Title.Trim(), 200);
            rr.ErrorMessage = null;
            await _Database.ResearchRequests.UpdateAsync(rr, token).ConfigureAwait(false);
            topic.Name = rr.Title;
            topic.Brief = plan.Brief.Trim();
            topic.SeedQueries = plan.SeedQueries.Where(q => !String.IsNullOrWhiteSpace(q)).Select(q => q.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
            await _Database.Topics.UpdateAsync(topic, token).ConfigureAwait(false);

            foreach (TopicDimension d in await _Refinement.DimensionsAsync(rr.TenantId, topic.Id, token).ConfigureAwait(false))
                if (d.Origin == OriginEnum.Model && !d.IsLocked) await _Database.TopicDimensions.DeleteAsync(rr.TenantId, d.Id, token).ConfigureAwait(false);
            int order = 0;
            foreach (ProposedDimension p in plan.Dimensions.Where(p => !String.IsNullOrWhiteSpace(p.Name)).Take(8))
            {
                await _Database.TopicDimensions.CreateAsync(new TopicDimension
                {
                    TenantId = rr.TenantId,
                    TopicId = topic.Id,
                    Name = Shorten(p.Name.Trim(), 200),
                    Description = p.Description,
                    Rationale = p.Rationale,
                    Weight = p.Weight,
                    IncludeTerms = p.IncludeTerms ?? new List<string>(),
                    ExcludeTerms = p.ExcludeTerms ?? new List<string>(),
                    SortOrder = order++
                }, token).ConfigureAwait(false);
            }

            foreach (TopicConstraint c in await _Refinement.ConstraintsAsync(rr.TenantId, topic.Id, token).ConfigureAwait(false))
                if (c.Origin == OriginEnum.Model && !c.IsLocked) await _Database.TopicConstraints.DeleteAsync(rr.TenantId, c.Id, token).ConfigureAwait(false);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await _Refinement.AddConstraintsAsync(topic, ConstraintKindEnum.IncludeKeyword, plan.IncludeKeywords, seen, token).ConfigureAwait(false);
            await _Refinement.AddConstraintsAsync(topic, ConstraintKindEnum.ExcludeKeyword, plan.ExcludeKeywords, seen, token).ConfigureAwait(false);

            if (!skipQuestions && session.Round == 0 && plan.Questions.Count > 0)
            {
                int i = 0;
                foreach (ProposedQuestion q in plan.Questions) await _Refinement.CreateQuestionAsync(session, 1, q, null, i++, token).ConfigureAwait(false);
                session.Round = 1;
                session.Step = RefinementStepEnum.Questions;
                await _Database.RefinementSessions.UpdateAsync(session, token).ConfigureAwait(false);
            }
        }

        private async Task<ResearchRequest> ReadAsync(string tenantId, string requestId, CancellationToken token)
        {
            return await _Database.ResearchRequests.ReadAsync(tenantId, requestId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Request " + requestId + " was not found.");
        }

        private async Task<RefinementSession> SessionAsync(string tenantId, ResearchRequest rr, CancellationToken token)
        {
            if (String.IsNullOrEmpty(rr.TopicId)) throw new KeyNotFoundException("The request has no plan.");
            return (await _Database.RefinementSessions.EnumerateAsync(new RefinementSessionFilter { TenantId = tenantId, TopicId = rr.TopicId, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault()
                ?? throw new KeyNotFoundException("The request session was not found.");
        }

        private static string Shorten(string value, int max)
        {
            string v = value.Trim();
            return v.Length > max ? v.Substring(0, max - 3).TrimEnd() + "..." : v;
        }

        #endregion
    }
}
