namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Serialization;

    /// <summary>
    /// Offline: a run cost report reads totals, stages, models, and tasks; a run and its stages read their cost fields;
    /// a model call reads its stage; the runStageId and sortBy filters are written; GetCostsAsync rejects an empty id.
    /// </summary>
    public class RunCostWireTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name
        {
            get
            {
                return "Offline: run and stage cost wire shapes";
            }
        }

        /// <summary>
        /// Offline.
        /// </summary>
        public override bool RequiresServer
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RunCostWireTest()
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
            string json = "{\"runId\":\"run_x\",\"topicId\":\"top_x\",\"status\":\"Completed\",\"currency\":\"USD\","
                + "\"totals\":{\"estimatedCostUsd\":0.4215,\"llmCallCount\":12,\"unpricedCallCount\":1,\"inputTokens\":90000,\"outputTokens\":8000,\"cachedInputTokens\":40000,\"reasoningTokens\":2000},"
                + "\"costPerNewFindingUsd\":0.0527,"
                + "\"stages\":[{\"runStageId\":\"stg_a\",\"stage\":\"Plan\",\"status\":\"Succeeded\",\"sortOrder\":0,\"estimatedCostUsd\":0.03,\"llmCallCount\":1,\"shareOfCost\":0.071},"
                + "{\"runStageId\":\"stg_b\",\"stage\":\"Notify\",\"status\":\"Succeeded\",\"sortOrder\":9,\"llmCallCount\":0}],"
                + "\"byModel\":[{\"modelEndpointId\":\"mdl_x\",\"provider\":\"Anthropic\",\"model\":\"claude-opus-5-5\",\"estimatedCostUsd\":0.4215,\"llmCallCount\":12}],"
                + "\"byTask\":[{\"task\":\"Synthesis\",\"estimatedCostUsd\":0.2}]}";
            RunCostReport? report = MyMorningNewsSerializer.Deserialize<RunCostReport>(json);
            Helpers.NotNull(report, "report");
            Helpers.Equal("run_x", report!.RunId, "run id");
            Helpers.Equal(RunStatusEnum.Completed, report.Status, "status");
            Helpers.Equal("USD", report.Currency, "currency");
            Helpers.Equal<decimal?>(0.4215m, report.Totals.EstimatedCostUsd, "total cost");
            Helpers.Equal(1, report.Totals.UnpricedCallCount, "unpriced calls");
            Helpers.Equal(40000, report.Totals.CachedInputTokens, "cached input tokens");
            Helpers.Equal<decimal?>(0.0527m, report.CostPerNewFindingUsd, "cost per new finding");
            Helpers.Equal(2, report.Stages.Count, "stages");
            Helpers.Equal("Plan", report.Stages[0].Stage, "stage name");
            Helpers.Equal<double?>(0.071, report.Stages[0].ShareOfCost, "share of cost");
            Helpers.True(report.Stages[1].EstimatedCostUsd == null && report.Stages[1].ShareOfCost == null, "a stage without model calls has no cost");
            Helpers.True(report.Unattributed == null, "unattributed is null when every call has a stage");
            Helpers.Equal("claude-opus-5-5", report.ByModel[0].Model, "model");
            Helpers.Equal(ModelTaskEnum.Synthesis, report.ByTask[0].Task, "task");

            Run? run = MyMorningNewsSerializer.Deserialize<Run>("{\"id\":\"run_x\",\"estimatedCostUsd\":0.4215,\"llmCallCount\":12,\"unpricedCallCount\":1,\"cachedInputTokens\":40000,\"reasoningTokens\":2000}");
            Helpers.NotNull(run, "run");
            Helpers.Equal<decimal?>(0.4215m, run!.EstimatedCostUsd, "run cost");
            Helpers.True(run.LlmCallCount == 12 && run.UnpricedCallCount == 1 && run.CachedInputTokens == 40000 && run.ReasoningTokens == 2000, "run counts");
            RunStage? stage = MyMorningNewsSerializer.Deserialize<RunStage>("{\"id\":\"stg_a\",\"stage\":\"Plan\",\"estimatedCostUsd\":0.03,\"llmCallCount\":1,\"inputTokens\":5000,\"outputTokens\":400}");
            Helpers.NotNull(stage, "stage");
            Helpers.True(stage!.EstimatedCostUsd == 0.03m && stage.LlmCallCount == 1 && stage.InputTokens == 5000 && stage.OutputTokens == 400, "stage cost fields");
            LlmCall? call = MyMorningNewsSerializer.Deserialize<LlmCall>("{\"id\":\"llm_x\",\"runId\":\"run_x\",\"runStageId\":\"stg_a\"}");
            Helpers.Equal("stg_a", Helpers.NotNull(call, "call").RunStageId, "call stage");

            Dictionary<string, string> callQuery = new LlmCallListOptions { RunId = "run_x", RunStageId = "stg_a" }.ToQuery();
            Helpers.Equal("stg_a", callQuery["runStageId"], "runStageId filter");
            Dictionary<string, string> runQuery = new RunListOptions { SortBy = "estimatedCostUsd", SortDirection = SortDirectionEnum.Descending }.ToQuery();
            Helpers.Equal("estimatedCostUsd", runQuery["sortBy"], "sort by cost");

            using (MyMorningNewsClient offline = new MyMorningNewsClient("http://127.0.0.1:1"))
            {
                bool threw = false;
                try
                {
                    await offline.Runs.GetCostsAsync(String.Empty, token).ConfigureAwait(false);
                }
                catch (ArgumentNullException)
                {
                    threw = true;
                }

                Helpers.True(threw, "GetCostsAsync rejects an empty id");
            }
        }
    }
}
