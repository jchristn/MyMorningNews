namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Finding pin and hide, evidence read, and a run's model calls. Skipped when the server predates these routes. Existing
    /// findings are pinned and unpinned, never deleted.
    /// </summary>
    public class FindingsEditTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Finding pin and hide, evidence, run model calls";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FindingsEditTest()
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
            EnumerationResult<Run> runs = await StepAsync(context, result, "GET runs", () => c.Runs.ListAsync(new RunListOptions { PageSize = 1 }, token)).ConfigureAwait(false);
            if (runs.Objects.Count == 0) throw new SkipTestException("No runs to read model calls from.");
            EnumerationResult<LlmCall> calls;
            try
            {
                calls = await c.Runs.ListLlmCallsAsync(runs.Objects[0].Id, new ListOptions { PageSize = 5 }, token).ConfigureAwait(false);
            }
            catch (MyMorningNewsException ex) when (ex.StatusCode == 404)
            {
                throw new SkipTestException("The server does not serve runs/{id}/llm-calls yet.");
            }

            Helpers.True(calls.Objects.All(x => x.RunId == runs.Objects[0].Id), "calls belong to the run");
            await StepAsync(context, result, "PUT unknown finding (404)", () => Helpers.ExpectErrorAsync(() => c.Findings.UpdateAsync("fnd_missing", new FindingUpdateRequest { Pinned = true }, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "GET unknown evidence (404)", () => Helpers.ExpectErrorAsync(() => c.Findings.GetEvidenceAsync("evd_missing", token), 404)).ConfigureAwait(false);

            EnumerationResult<Finding> findings = await StepAsync(context, result, "GET findings (include hidden)", () => c.Findings.ListAsync(new FindingListOptions { IncludeHidden = true, PageSize = 1 }, token)).ConfigureAwait(false);
            Finding? f = findings.Objects.FirstOrDefault();
            if (f == null) return;
            bool wasPinned = f.Pinned;
            Finding toggled = await StepAsync(context, result, "PUT finding pinned", () => c.Findings.UpdateAsync(f.Id, new FindingUpdateRequest { Pinned = !wasPinned }, token)).ConfigureAwait(false);
            Helpers.Equal(!wasPinned, toggled.Pinned, "pinned toggled");
            await StepAsync(context, result, "PUT finding pinned (restore)", () => c.Findings.UpdateAsync(f.Id, new FindingUpdateRequest { Pinned = wasPinned }, token)).ConfigureAwait(false);
            FindingDetailResponse detail = await StepAsync(context, result, "GET finding", () => c.Findings.GetAsync(f.Id, token)).ConfigureAwait(false);
            if (detail.Evidence.Count > 0)
            {
                EvidenceView ev = await StepAsync(context, result, "GET evidence", () => c.Findings.GetEvidenceAsync(detail.Evidence[0].Evidence.Id, token)).ConfigureAwait(false);
                Helpers.Equal(detail.Evidence[0].Evidence.Id, ev.Evidence.Id, "evidence id");
            }
        }
    }
}
