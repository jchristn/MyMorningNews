namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Model endpoints against an unreachable local address (nothing is billed): CRUD, validate, list models (502), test,
    /// a topic assignment override, model calls, and 404 after delete.
    /// </summary>
    public class ModelEndpointsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Model endpoints CRUD, validate, models, test, assignments, llm calls";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelEndpointsTest()
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
            await StepAsync(context, result, "POST model endpoint without a provider (400)", () => Helpers.ExpectErrorAsync(() => c.ModelEndpoints.CreateAsync(new ModelEndpointUpsertRequest { Name = "x" }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            ModelEndpoint e = await StepAsync(context, result, "POST model endpoint", () => c.ModelEndpoints.CreateAsync(new ModelEndpointUpsertRequest
            {
                Name = context.Name("model"),
                Provider = ModelProviderEnum.OpenAiCompatible,
                Endpoint = "http://127.0.0.1:9/v1",
                Model = "harness-model",
                SupportsChat = true,
                SupportsEmbeddings = false,
                TimeoutMs = 2000,
                Active = false
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("model:" + e.Id, ct => c.ModelEndpoints.DeleteAsync(e.Id, ct));

            ModelEndpoint read = await StepAsync(context, result, "GET model endpoint", () => c.ModelEndpoints.GetAsync(e.Id, token)).ConfigureAwait(false);
            Helpers.Equal("harness-model", read.Model, "model");
            ModelEndpoint updated = await StepAsync(context, result, "PUT model endpoint", () => c.ModelEndpoints.UpdateAsync(e.Id, new ModelEndpointUpsertRequest { MaxTokens = 512 }, token)).ConfigureAwait(false);
            ModelEndpoint tuned = await StepAsync(context, result, "PUT model endpoint (optional values)", () => c.ModelEndpoints.UpdateAsync(e.Id, new ModelEndpointUpsertRequest { Temperature = 0.3, ContextLength = 4096 }, token)).ConfigureAwait(false);
            Helpers.True(tuned.Temperature == 0.3 && tuned.ContextLength == 4096, "optional values set");
            ModelEndpoint cleared = await StepAsync(context, result, "PUT model endpoint (clearFields)", () => c.ModelEndpoints.UpdateAsync(e.Id, new ModelEndpointUpsertRequest { ClearFields = new List<string> { "temperature", "contextLength" } }, token)).ConfigureAwait(false);
            Helpers.True(cleared.Temperature == null && cleared.ContextLength == null && cleared.MaxTokens == 512, "clearFields sets optional values to null");
            await StepAsync(context, result, "PUT model endpoint clearFields=name (400)", () => Helpers.ExpectErrorAsync(() => c.ModelEndpoints.UpdateAsync(e.Id, new ModelEndpointUpsertRequest { ClearFields = new List<string> { "name" } }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            await StepAsync(context, result, "PUT model endpoint value and clearFields (400)", () => Helpers.ExpectErrorAsync(() => c.ModelEndpoints.UpdateAsync(e.Id, new ModelEndpointUpsertRequest { ContextLength = 1, ClearFields = new List<string> { "contextLength" } }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            Helpers.Equal(512, updated.MaxTokens, "max tokens");
            EnumerationResult<ModelEndpoint> list = await StepAsync(context, result, "GET model endpoints (provider filter)",
                () => c.ModelEndpoints.ListAsync(new ModelEndpointListOptions { Provider = ModelProviderEnum.OpenAiCompatible, PageSize = 100 }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Objects, x => x.Id == e.Id, "the endpoint");

            ModelValidationResponse v = await StepAsync(context, result, "POST validate (unreachable)", () => c.ModelEndpoints.ValidateAsync(e.Id, token)).ConfigureAwait(false);
            Helpers.True(!v.Reachable, "an unreachable endpoint is reported");
            Helpers.True(!v.Succeeded, "an unreachable endpoint does not succeed");
            ModelEndpoint checkedEndpoint = await StepAsync(context, result, "GET model endpoint (after validate)", () => c.ModelEndpoints.GetAsync(e.Id, token)).ConfigureAwait(false);
            Helpers.True(checkedEndpoint.LastValidationSucceeded == false, "the failed validation is recorded");
            await StepAsync(context, result, "GET models (502)", () => Helpers.ExpectErrorAsync(() => c.ModelEndpoints.ListModelsAsync(e.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token), 502, "UpstreamError")).ConfigureAwait(false);
            ModelTestResponse t = await StepAsync(context, result, "POST test (unreachable)", () => c.ModelEndpoints.TestAsync(e.Id, new ModelTestRequest { Prompt = "Say hi." }, token)).ConfigureAwait(false);
            Helpers.True(!t.Success && !System.String.IsNullOrEmpty(t.Error), "a failed test carries an error");

            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "model", token)).ConfigureAwait(false);
            ModelAssignment a = await StepAsync(context, result, "PUT model-assignment (topic override)", () => c.ModelEndpoints.SetAssignmentAsync(new ModelAssignmentUpsertRequest
            {
                Task = ModelTaskEnum.QueryPlanning,
                TopicId = topic.Id,
                ModelEndpointId = e.Id
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("assignment:" + a.Id, ct => c.ModelEndpoints.DeleteAssignmentAsync(a.Id, ct));
            EnumerationResult<ModelAssignment> assignments = await StepAsync(context, result, "GET model-assignments (topic filter)",
                () => c.ModelEndpoints.ListAssignmentsAsync(new ModelAssignmentListOptions { TopicId = topic.Id }, token)).ConfigureAwait(false);
            Helpers.Contains(assignments.Objects, x => x.Id == a.Id, "the assignment");
            await StepAsync(context, result, "DELETE model-assignment", () => c.ModelEndpoints.DeleteAssignmentAsync(a.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("assignment:" + a.Id);
            await StepAsync(context, result, "PUT model-assignment with an unknown endpoint (400)", () => Helpers.ExpectErrorAsync(() => c.ModelEndpoints.SetAssignmentAsync(new ModelAssignmentUpsertRequest
            {
                Task = ModelTaskEnum.QueryPlanning,
                TopicId = topic.Id,
                ModelEndpointId = "mep_missing"
            }, token), 400)).ConfigureAwait(false);

            EnumerationResult<LlmCall> calls = await StepAsync(context, result, "GET llm-calls (dry runs for the endpoint)",
                () => c.LlmCalls.ListAsync(new LlmCallListOptions { ModelEndpointId = e.Id, PageSize = 5 }, token)).ConfigureAwait(false);
            if (calls.Objects.Count > 0)
            {
                Helpers.True(calls.Objects[0].PromptText == null, "lists leave payloads out");
                LlmCall call = await StepAsync(context, result, "GET llm-call", () => c.LlmCalls.GetAsync(calls.Objects[0].Id, token)).ConfigureAwait(false);
                Helpers.Equal(e.Id, call.ModelEndpointId, "call endpoint");
            }

            await StepAsync(context, result, "GET unknown llm-call (404)", () => Helpers.ExpectErrorAsync(() => c.LlmCalls.GetAsync("llm_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE model endpoint", () => c.ModelEndpoints.DeleteAsync(e.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("model:" + e.Id);
            await StepAsync(context, result, "GET deleted model endpoint (404)", () => Helpers.ExpectErrorAsync(() => c.ModelEndpoints.GetAsync(e.Id, token), 404)).ConfigureAwait(false);
        }
    }
}
