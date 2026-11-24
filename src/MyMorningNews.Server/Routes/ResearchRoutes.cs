namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Research results (plan sections 16 to 20): findings with evidence, run stages, queries, and results, predictions
    /// and their reviews, suggestions, and fetched sources. Members see their own topics' results.
    /// </summary>
    public class ResearchRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public ResearchRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/findings", ListFindingsAsync, null, openApiMetadata: Paged(Doc("List findings", "Findings",
                "Defaults: no duplicates, no dry runs, and no below-threshold findings unless tier=BelowThreshold, includeBelowThreshold=true, or includeDuplicates=true (duplicates are below threshold).").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("runId", typeof(string)))
                .WithParameter(QueryParam("tier", typeof(FindingTierEnum))).WithParameter(QueryParam("category", typeof(FindingCategoryEnum))).WithParameter(QueryParam("includeDuplicates", typeof(bool)))
                .WithParameter(QueryParam("includeDryRun", typeof(bool))).WithParameter(QueryParam("minScore", typeof(int))).WithParameter(QueryParam("dimensionId", typeof(string)))
                .WithParameter(QueryParam("includeBelowThreshold", typeof(bool))).WithParameter(QueryParam("includeHidden", typeof(bool))).WithParameter(QueryParam("pinned", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/findings" + "/enumerate", ctx => EnumerateAsync(ctx, ListFindingsAsync), null, openApiMetadata: Paged(Doc("List findings", "Findings",
                "Defaults: no duplicates, no dry runs, and no below-threshold findings unless tier=BelowThreshold, includeBelowThreshold=true, or includeDuplicates=true (duplicates are below threshold).").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("runId", typeof(string)))
                .WithParameter(QueryParam("tier", typeof(FindingTierEnum))).WithParameter(QueryParam("category", typeof(FindingCategoryEnum))).WithParameter(QueryParam("includeDuplicates", typeof(bool)))
                .WithParameter(QueryParam("includeDryRun", typeof(bool))).WithParameter(QueryParam("minScore", typeof(int))).WithParameter(QueryParam("dimensionId", typeof(string)))
                .WithParameter(QueryParam("includeBelowThreshold", typeof(bool))).WithParameter(QueryParam("includeHidden", typeof(bool))).WithParameter(QueryParam("pinned", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/findings/{id}", ReadFindingAsync, null, openApiMetadata: Doc("Read a finding with its evidence", "Findings"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/findings/{id}", UpdateFindingAsync, null, openApiMetadata: Doc("Pin or hide a finding", "Findings",
                "Hidden findings are left out of lists (unless includeHidden=true), briefings, and emails.").WithRequestBody(Body<FindingUpdateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/findings/{id}", DeleteFindingAsync, null, openApiMetadata: Doc("Delete a finding", "Findings",
                "Removes the finding and its evidence links. Shared evidence and sources stay. Ratings of it stay in history."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/evidence/{id}", ReadEvidenceAsync, null, openApiMetadata: Doc("Read one piece of evidence with its source", "Findings"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/llm-calls", RunCallsAsync, null, openApiMetadata: Paged(Doc("A run's model calls, oldest first", "Runs").WithParameter(QueryParam("runStageId", typeof(string)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/llm-calls" + "/enumerate", ctx => EnumerateAsync(ctx, RunCallsAsync), null, openApiMetadata: Paged(Doc("A run's model calls, oldest first", "Runs").WithParameter(QueryParam("runStageId", typeof(string)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/stages", StagesAsync, null, openApiMetadata: Paged(Doc("A run's stages", "Runs")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/stages" + "/enumerate", ctx => EnumerateAsync(ctx, StagesAsync), null, openApiMetadata: Paged(Doc("A run's stages", "Runs")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/queries", QueriesAsync, null, openApiMetadata: Paged(Doc("A run's search queries", "Runs")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/queries" + "/enumerate", ctx => EnumerateAsync(ctx, QueriesAsync), null, openApiMetadata: Paged(Doc("A run's search queries", "Runs")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/runs/{id}/results", ResultsAsync, null, openApiMetadata: Paged(Doc("A run's search results with dispositions", "Runs")
                .WithParameter(QueryParam("disposition", typeof(SearchResultDispositionEnum)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/runs/{id}/results" + "/enumerate", ctx => EnumerateAsync(ctx, ResultsAsync), null, openApiMetadata: Paged(Doc("A run's search results with dispositions", "Runs")
                .WithParameter(QueryParam("disposition", typeof(SearchResultDispositionEnum)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/predictions", ListPredictionsAsync, null, openApiMetadata: Paged(Doc("List predictions", "Predictions", "Sort by horizonUtc, createdUtc, confidence, status, or brierScore.").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(PredictionStatusEnum))).WithParameter(QueryParam("includeDryRun", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/predictions" + "/enumerate", ctx => EnumerateAsync(ctx, ListPredictionsAsync), null, openApiMetadata: Paged(Doc("List predictions", "Predictions", "Sort by horizonUtc, createdUtc, confidence, status, or brierScore.").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(PredictionStatusEnum))).WithParameter(QueryParam("includeDryRun", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/predictions/{id}", ReadPredictionAsync, null, openApiMetadata: Doc("Read a prediction with its reviews", "Predictions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/predictions/{id}/resolve", ResolveAsync, null, openApiMetadata: Doc("Resolve a prediction", "Predictions").WithRequestBody(Body<PredictionResolveRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/prediction-reviews/{id}/accept", AcceptReviewAsync, null, openApiMetadata: Doc("Accept a proposed resolution", "Predictions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/prediction-reviews/{id}/reject", RejectReviewAsync, null, openApiMetadata: Doc("Reject a proposed resolution", "Predictions"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/suggestions", ListSuggestionsAsync, null, openApiMetadata: Paged(Doc("List suggestions", "Suggestions", "Sort by createdUtc, priority, status, or title.").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(SuggestionStatusEnum))).WithParameter(QueryParam("priority", typeof(SuggestionPriorityEnum)))
                .WithParameter(QueryParam("includeDryRun", typeof(bool)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/suggestions" + "/enumerate", ctx => EnumerateAsync(ctx, ListSuggestionsAsync), null, openApiMetadata: Paged(Doc("List suggestions", "Suggestions", "Sort by createdUtc, priority, status, or title.").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool)))
                .WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("status", typeof(SuggestionStatusEnum))).WithParameter(QueryParam("priority", typeof(SuggestionPriorityEnum)))
                .WithParameter(QueryParam("includeDryRun", typeof(bool)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/suggestions/{id}", ReadSuggestionAsync, null, openApiMetadata: Doc("Read a suggestion", "Suggestions"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/suggestions/{id}/status", SuggestionStatusAsync, null, openApiMetadata: Doc("Accept, snooze, dismiss, or complete a suggestion", "Suggestions")
                .WithRequestBody(Body<SuggestionStatusRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/sources", ListSourcesAsync, null, openApiMetadata: Paged(Doc("List fetched sources for my topics", "Sources",
                "Newest first; content text is left out (read one source for it).").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("runId", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/sources" + "/enumerate", ctx => EnumerateAsync(ctx, ListSourcesAsync), null, openApiMetadata: Paged(Doc("List fetched sources for my topics", "Sources",
                "Newest first; content text is left out (read one source for it).").WithParameter(QueryParam("userId", typeof(string))).WithParameter(QueryParam("mine", typeof(bool))).WithParameter(QueryParam("topicId", typeof(string))).WithParameter(QueryParam("runId", typeof(string)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/sources/{id}", ReadSourceAsync, null, openApiMetadata: Doc("Read a fetched source", "Sources"));
        }

        private async Task ListFindingsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FindingRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            FindingFilter filter = new FindingFilter
            {
                TenantId = tenantId,
                UserId = ListScope(ctx),
                TopicId = Query(ctx, "topicId"),
                RunId = Query(ctx, "runId"),
                Tier = QueryEnum<FindingTierEnum>(ctx, "tier"),
                Category = QueryEnum<FindingCategoryEnum>(ctx, "category"),
                IsDuplicate = QueryBool(ctx, "includeDuplicates") == true ? null : false,
                IsDryRun = QueryBool(ctx, "includeDryRun") == true ? null : false,
                DimensionId = Query(ctx, "dimensionId"),
                MinRelevanceScore = QueryInt(ctx, "minScore"),
                Hidden = QueryBool(ctx, "includeHidden") == true ? null : false,
                Pinned = QueryBool(ctx, "pinned")
            };
            if (!filter.Tier.HasValue && QueryBool(ctx, "includeBelowThreshold") != true && QueryBool(ctx, "includeDuplicates") != true) filter.ExcludeTier = FindingTierEnum.BelowThreshold;
            ApplyPaging(ctx, filter);
            if (String.IsNullOrEmpty(filter.SortBy)) filter.SortBy = "RelevanceScore";
            EnumerationResult<Finding> page = await Server.Database.Findings.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task ReadFindingAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FindingRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Finding? f = await Server.Database.Findings.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (f == null || !IsOwnerOrAdmin(ctx, f.UserId)) { await SendNotFoundAsync(ctx, "Finding").ConfigureAwait(false); return; }
            FindingDetailResponse detail = new FindingDetailResponse { Finding = f };
            if (!String.IsNullOrEmpty(f.DimensionId)) detail.Dimension = await Server.Database.TopicDimensions.ReadAsync(tenantId, f.DimensionId, ctx.Token).ConfigureAwait(false);
            detail.TopicName = (await Server.Database.Topics.ReadAsync(tenantId, f.TopicId, ctx.Token).ConfigureAwait(false))?.Name;
            foreach (FindingEvidence link in (await Server.Database.FindingEvidence.EnumerateAsync(new FindingEvidenceFilter { TenantId = tenantId, FindingId = f.Id, PageSize = 50 }, ctx.Token).ConfigureAwait(false)).Objects.OrderBy(l => l.SortOrder))
            {
                Evidence? e = await Server.Database.Evidence.ReadAsync(tenantId, link.EvidenceId, ctx.Token).ConfigureAwait(false);
                if (e == null) continue;
                SourceItem? s = await Server.Database.SourceItems.ReadAsync(tenantId, e.SourceItemId, ctx.Token).ConfigureAwait(false);
                detail.Evidence.Add(new EvidenceView { Evidence = e, SourceUrl = s?.CanonicalUrl, SourceTitle = s?.Title, SourceDomain = s?.Domain, SourcePublishedUtc = s?.PublishedUtc, Role = link.Role.ToString() });
            }

            await SendJsonAsync(ctx, 200, detail).ConfigureAwait(false);
        }

        private async Task UpdateFindingAsync(HttpContextBase ctx)
        {
            Finding? f = await OwnedFindingAsync(ctx, RequestTypeEnum.FindingWrite).ConfigureAwait(false);
            if (f == null) return;
            FindingUpdateRequest? req = await ReadBodyAsync<FindingUpdateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (!req.Pinned.HasValue && !req.Hidden.HasValue)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Send pinned, hidden, or both.").ConfigureAwait(false);
                return;
            }

            if (req.Pinned.HasValue) f.Pinned = req.Pinned.Value;
            if (req.Hidden.HasValue) f.Hidden = req.Hidden.Value;
            await SendJsonAsync(ctx, 200, await Server.Database.Findings.UpdateAsync(f, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteFindingAsync(HttpContextBase ctx)
        {
            Finding? f = await OwnedFindingAsync(ctx, RequestTypeEnum.FindingDelete).ConfigureAwait(false);
            if (f == null) return;
            foreach (FindingEvidence link in (await Server.Database.FindingEvidence.EnumerateAsync(new FindingEvidenceFilter { TenantId = f.TenantId, FindingId = f.Id, PageSize = 1000 }, ctx.Token).ConfigureAwait(false)).Objects)
                await Server.Database.FindingEvidence.DeleteAsync(f.TenantId, link.Id, ctx.Token).ConfigureAwait(false);
            await Server.Database.Findings.DeleteAsync(f.TenantId, f.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ReadEvidenceAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.FindingRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Evidence? e = await Server.Database.Evidence.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (e == null || !await EvidenceVisibleAsync(ctx, e).ConfigureAwait(false)) { await SendNotFoundAsync(ctx, "Evidence").ConfigureAwait(false); return; }
            SourceItem? s = await Server.Database.SourceItems.ReadAsync(tenantId, e.SourceItemId, ctx.Token).ConfigureAwait(false);
            FindingEvidence? link = (await Server.Database.FindingEvidence.EnumerateAsync(new FindingEvidenceFilter { TenantId = tenantId, EvidenceId = e.Id, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
            EvidenceView view = new EvidenceView { Evidence = e, SourceUrl = s?.CanonicalUrl, SourceTitle = s?.Title, SourceDomain = s?.Domain, SourcePublishedUtc = s?.PublishedUtc };
            if (link != null) view.Role = link.Role.ToString();
            await SendJsonAsync(ctx, 200, view).ConfigureAwait(false);
        }

        private async Task RunCallsAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx).ConfigureAwait(false);
            if (run == null) return;
            LlmCallFilter filter = new LlmCallFilter { TenantId = run.TenantId, RunId = run.Id, RunStageId = Query(ctx, "runStageId") };
            ApplyPaging(ctx, filter);
            if (String.IsNullOrEmpty(Query(ctx, "sortDirection"))) filter.SortDirection = SortDirectionEnum.Ascending;
            await SendJsonAsync(ctx, 200, await Server.Database.LlmCalls.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task<Finding?> OwnedFindingAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Finding? f = await Server.Database.Findings.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (f == null || !IsOwnerOrAdmin(ctx, f.UserId))
            {
                await SendNotFoundAsync(ctx, "Finding").ConfigureAwait(false);
                return null;
            }

            return f;
        }

        private async Task<bool> EvidenceVisibleAsync(HttpContextBase ctx, Evidence e)
        {
            if (!String.IsNullOrEmpty(e.RunId))
            {
                Run? run = await Server.Database.Runs.ReadAsync(e.TenantId, e.RunId, ctx.Token).ConfigureAwait(false);
                if (run != null) return IsOwnerOrAdmin(ctx, run.UserId);
            }

            foreach (FindingEvidence link in (await Server.Database.FindingEvidence.EnumerateAsync(new FindingEvidenceFilter { TenantId = e.TenantId, EvidenceId = e.Id, PageSize = 20 }, ctx.Token).ConfigureAwait(false)).Objects)
            {
                Finding? f = await Server.Database.Findings.ReadAsync(e.TenantId, link.FindingId, ctx.Token).ConfigureAwait(false);
                if (f != null && IsOwnerOrAdmin(ctx, f.UserId)) return true;
            }

            return false;
        }

        private async Task StagesAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx).ConfigureAwait(false);
            if (run == null) return;
            RunStageFilter query = new RunStageFilter { TenantId = run.TenantId, RunId = run.Id, SortBy = "sortOrder", SortDirection = SortDirectionEnum.Ascending };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.RunStages.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task QueriesAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx).ConfigureAwait(false);
            if (run == null) return;
            SearchQueryFilter query = new SearchQueryFilter { TenantId = run.TenantId, RunId = run.Id };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.SearchQueries.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ResultsAsync(HttpContextBase ctx)
        {
            Run? run = await OwnedRunAsync(ctx).ConfigureAwait(false);
            if (run == null) return;
            SearchResultFilter query = new SearchResultFilter { TenantId = run.TenantId, RunId = run.Id, Disposition = QueryEnum<SearchResultDispositionEnum>(ctx, "disposition") };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.SearchResults.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListPredictionsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PredictionRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PredictionFilter filter = new PredictionFilter { TenantId = tenantId, UserId = ListScope(ctx), TopicId = Query(ctx, "topicId"), Status = QueryEnum<PredictionStatusEnum>(ctx, "status"), IsDryRun = QueryBool(ctx, "includeDryRun") == true ? null : false };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Predictions.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadPredictionAsync(HttpContextBase ctx)
        {
            Prediction? p = await OwnedPredictionAsync(ctx, RequestTypeEnum.PredictionRead, Param(ctx, "id")).ConfigureAwait(false);
            if (p == null) return;
            List<PredictionReview> reviews = (await Server.Database.PredictionReviews.EnumerateAsync(new PredictionReviewFilter { TenantId = p.TenantId, PredictionId = p.Id, PageSize = 100 }, ctx.Token).ConfigureAwait(false)).Objects
                .OrderByDescending(r => r.CreatedUtc).ToList();
            await SendJsonAsync(ctx, 200, new PredictionDetailResponse { Prediction = p, Reviews = reviews }).ConfigureAwait(false);
        }

        private async Task ResolveAsync(HttpContextBase ctx)
        {
            Prediction? p = await OwnedPredictionAsync(ctx, RequestTypeEnum.PredictionWrite, Param(ctx, "id")).ConfigureAwait(false);
            if (p == null) return;
            PredictionResolveRequest? req = await ReadBodyAsync<PredictionResolveRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (req.Status == PredictionStatusEnum.Open || req.Status == PredictionStatusEnum.ProposedResolved)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Resolve with Accurate, PartiallyAccurate, Inaccurate, Unresolvable, or Withdrawn.").ConfigureAwait(false);
                return;
            }

            Apply(p, req.Status, "User", req.Note);
            await SendJsonAsync(ctx, 200, await Server.Database.Predictions.UpdateAsync(p, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task AcceptReviewAsync(HttpContextBase ctx)
        {
            await DecideReviewAsync(ctx, true).ConfigureAwait(false);
        }

        private async Task RejectReviewAsync(HttpContextBase ctx)
        {
            await DecideReviewAsync(ctx, false).ConfigureAwait(false);
        }

        private async Task DecideReviewAsync(HttpContextBase ctx, bool accept)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PredictionReview? review = await Server.Database.PredictionReviews.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            Prediction? p = review == null ? null : await OwnedPredictionAsync(ctx, RequestTypeEnum.PredictionWrite, review.PredictionId).ConfigureAwait(false);
            if (review == null || p == null)
            {
                if (review == null) await SendNotFoundAsync(ctx, "Prediction review").ConfigureAwait(false);
                return;
            }

            review.AcceptedByUser = accept;
            await Server.Database.PredictionReviews.UpdateAsync(review, ctx.Token).ConfigureAwait(false);
            if (accept) Apply(p, review.ProposedStatus, "AgentConfirmed", review.Rationale);
            else if (p.Status == PredictionStatusEnum.ProposedResolved) p.Status = PredictionStatusEnum.Open;
            await SendJsonAsync(ctx, 200, await Server.Database.Predictions.UpdateAsync(p, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListSuggestionsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SuggestionRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            SuggestionFilter filter = new SuggestionFilter { TenantId = tenantId, UserId = ListScope(ctx), TopicId = Query(ctx, "topicId"), Status = QueryEnum<SuggestionStatusEnum>(ctx, "status"), Priority = QueryEnum<SuggestionPriorityEnum>(ctx, "priority"), IsDryRun = QueryBool(ctx, "includeDryRun") == true ? null : false };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Suggestions.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ReadSuggestionAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SuggestionRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Suggestion? s = await Server.Database.Suggestions.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (s == null || !IsOwnerOrAdmin(ctx, s.UserId)) { await SendNotFoundAsync(ctx, "Suggestion").ConfigureAwait(false); return; }
            await SendJsonAsync(ctx, 200, s).ConfigureAwait(false);
        }

        private async Task SuggestionStatusAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SuggestionWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            Suggestion? s = await Server.Database.Suggestions.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (s == null || !IsOwnerOrAdmin(ctx, s.UserId)) { await SendNotFoundAsync(ctx, "Suggestion").ConfigureAwait(false); return; }
            SuggestionStatusRequest? req = await ReadBodyAsync<SuggestionStatusRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (req.Status == SuggestionStatusEnum.Snoozed && (!req.SnoozedUntilUtc.HasValue || req.SnoozedUntilUtc.Value <= DateTime.UtcNow))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Snoozing needs a future snoozedUntilUtc.").ConfigureAwait(false);
                return;
            }

            s.Status = req.Status;
            s.SnoozedUntilUtc = req.Status == SuggestionStatusEnum.Snoozed ? DateTime.SpecifyKind(req.SnoozedUntilUtc!.Value.ToUniversalTime(), DateTimeKind.Utc) : null;
            s.StatusChangedUtc = DateTime.UtcNow;
            await SendJsonAsync(ctx, 200, await Server.Database.Suggestions.UpdateAsync(s, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListSourcesAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SourceRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            EnumerationQuery paging = new EnumerationQuery();
            ApplyPaging(ctx, paging);
            EnumerationResult<SourceItem> page = await Server.Database.SourceItems.ListForTopicsAsync(tenantId, ListScope(ctx), Query(ctx, "topicId"), Query(ctx, "runId"), Query(ctx, "search"), paging.PageNumber, paging.PageSize, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, page).ConfigureAwait(false);
        }

        private async Task ReadSourceAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SourceRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            SourceItem? s = await Server.Database.SourceItems.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            RequestContext rc = Context(ctx);
            bool admin = rc.IsAdmin || rc.IsTenantAdmin;
            if (s == null || (!admin && (String.IsNullOrEmpty(rc.UserId) || !await Server.Database.SourceItems.IsVisibleToUserAsync(tenantId, rc.UserId, s.Id, ctx.Token).ConfigureAwait(false))))
            {
                await SendNotFoundAsync(ctx, "Source").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, s).ConfigureAwait(false);
        }


        private async Task<Run?> OwnedRunAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RunRead).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Run? run = await Server.Database.Runs.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (run == null || !IsOwnerOrAdmin(ctx, run.UserId)) { await SendNotFoundAsync(ctx, "Run").ConfigureAwait(false); return null; }
            return run;
        }

        private async Task<Prediction?> OwnedPredictionAsync(HttpContextBase ctx, RequestTypeEnum type, string? id)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Prediction? p = await Server.Database.Predictions.ReadAsync(tenantId, id ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (p == null || !IsOwnerOrAdmin(ctx, p.UserId)) { await SendNotFoundAsync(ctx, "Prediction").ConfigureAwait(false); return null; }
            return p;
        }

        private static void Apply(Prediction p, PredictionStatusEnum status, string by, string? note)
        {
            p.Status = status;
            p.ResolvedUtc = DateTime.UtcNow;
            p.ResolvedBy = by;
            p.ResolutionNote = note;
            double? outcome = status == PredictionStatusEnum.Accurate ? 1.0 : status == PredictionStatusEnum.PartiallyAccurate ? 0.5 : status == PredictionStatusEnum.Inaccurate ? 0.0 : null;
            p.BrierScore = outcome.HasValue ? Math.Round(Math.Pow(p.Confidence - outcome.Value, 2), 4) : null;
        }
    }
}
