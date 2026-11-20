namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Read routes for research output: findings, sources, predictions, suggestions, and one-time requests, with 404s for
    /// unknown identifiers and typed validation errors.
    /// </summary>
    public class ResearchReadTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Findings, sources, predictions, suggestions, requests (read)";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ResearchReadTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            EnumerationResult<Finding> findings = await StepAsync(context, result, "GET findings (filters)",
                () => c.Findings.ListAsync(new FindingListOptions { MinScore = 0, IncludeBelowThreshold = true, PageSize = 5 }, token)).ConfigureAwait(false);
            if (findings.Items.Count > 0)
            {
                FindingDetailResponse f = await StepAsync(context, result, "GET finding", () => c.Findings.GetAsync(findings.Items[0].Id, token)).ConfigureAwait(false);
                Helpers.Equal(findings.Items[0].Id, f.Finding.Id, "finding id");
            }

            await StepAsync(context, result, "GET unknown finding (404)", () => Helpers.ExpectErrorAsync(() => c.Findings.GetAsync("fnd_missing", token), 404)).ConfigureAwait(false);

            EnumerationResult<SourceItem> sources = await StepAsync(context, result, "GET sources", () => c.Sources.ListAsync(new SourceListOptions { PageSize = 5 }, token)).ConfigureAwait(false);
            if (sources.Items.Count > 0)
                await StepAsync(context, result, "GET source", () => c.Sources.GetAsync(sources.Items[0].Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET unknown source (404)", () => Helpers.ExpectErrorAsync(() => c.Sources.GetAsync("src_missing", token), 404)).ConfigureAwait(false);

            EnumerationResult<Prediction> predictions = await StepAsync(context, result, "GET predictions", () => c.Predictions.ListAsync(new PredictionListOptions { PageSize = 5 }, token)).ConfigureAwait(false);
            if (predictions.Items.Count > 0)
                await StepAsync(context, result, "GET prediction", () => c.Predictions.GetAsync(predictions.Items[0].Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "POST resolve unknown prediction (404)", () => Helpers.ExpectErrorAsync(() => c.Predictions.ResolveAsync("prd_missing",
                new PredictionResolveRequest { Status = PredictionStatusEnum.Accurate }, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "POST accept unknown review (404)", () => Helpers.ExpectErrorAsync(() => c.Predictions.AcceptReviewAsync("prv_missing", token), 404)).ConfigureAwait(false);

            EnumerationResult<Suggestion> suggestions = await StepAsync(context, result, "GET suggestions", () => c.Suggestions.ListAsync(new SuggestionListOptions { PageSize = 5 }, token)).ConfigureAwait(false);
            if (suggestions.Items.Count > 0)
            {
                Suggestion s = suggestions.Items[0];
                await StepAsync(context, result, "GET suggestion", () => c.Suggestions.GetAsync(s.Id, token)).ConfigureAwait(false);
                await StepAsync(context, result, "PUT suggestion status Snoozed without a time (400)", () => Helpers.ExpectErrorAsync(() => c.Suggestions.SetStatusAsync(s.Id,
                    new SuggestionStatusRequest { Status = SuggestionStatusEnum.Snoozed }, token), 400)).ConfigureAwait(false);
            }

            await StepAsync(context, result, "PUT unknown suggestion status (404)", () => Helpers.ExpectErrorAsync(() => c.Suggestions.SetStatusAsync("sug_missing",
                new SuggestionStatusRequest { Status = SuggestionStatusEnum.Dismissed }, token), 404)).ConfigureAwait(false);

            EnumerationResult<ResearchRequest> requests = await StepAsync(context, result, "GET requests", () => c.Requests.ListAsync(new ResearchRequestListOptions { PageSize = 5 }, token)).ConfigureAwait(false);
            if (requests.Items.Count > 0)
            {
                ResearchRequestResponse rr = await StepAsync(context, result, "GET request", () => c.Requests.GetAsync(requests.Items.First().Id, token)).ConfigureAwait(false);
                Helpers.NotNull(rr, "request detail");
            }

            await StepAsync(context, result, "GET unknown request (404)", () => Helpers.ExpectErrorAsync(() => c.Requests.GetAsync("req_missing", token), 404)).ConfigureAwait(false);
        }
    }
}
