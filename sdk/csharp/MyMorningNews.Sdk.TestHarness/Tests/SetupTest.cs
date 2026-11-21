namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Workspace setup without billed calls: the administrator reads the status, stores a key for an unreachable local
    /// endpoint (the live check fails, so the endpoint reports LastValidationSucceeded false and the check is not Ready),
    /// gets 404 for an unknown endpoint, and a plain user reads the status with CanConfigure false and is refused verify and
    /// model-key with 403. The administrator's VerifyAsync is not called because it probes the real providers.
    /// </summary>
    public class SetupTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Setup status, model key, and 403 for non-administrators";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SetupTest()
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
            SetupStatusResponse status = await StepAsync(context, result, "GET setup (admin)", () => c.Setup.GetStatusAsync(token)).ConfigureAwait(false);
            Helpers.True(status.CanConfigure, "the administrator can configure");
            Helpers.True(status.Checks.Any(), "status lists checks");
            Helpers.True(status.Checks.All(x => !String.IsNullOrEmpty(x.Key)), "every check has a key");
            Helpers.True(status.Checks.Any(x => x.Kind == SetupCheckKindEnum.Search), "status has a search check");
            Helpers.Equal(status.Checks.Where(x => x.Required).All(x => x.State == SetupCheckStateEnum.Ready), status.Ready, "ready matches the required checks");

            await StepAsync(context, result, "POST setup/model-key with an unknown endpoint (404)",
                () => Helpers.ExpectErrorAsync(() => c.Setup.SetModelKeyAsync("mdl_missing", "sk-harness", token), 404)).ConfigureAwait(false);

            ModelEndpoint e = await StepAsync(context, result, "POST model endpoint (unreachable)", () => c.ModelEndpoints.CreateAsync(new ModelEndpointUpsertRequest
            {
                Name = context.Name("setup"),
                Provider = ModelProviderEnum.OpenAiCompatible,
                Endpoint = "http://127.0.0.1:9/v1",
                Model = "harness-model",
                SupportsChat = true,
                SupportsEmbeddings = false,
                TimeoutMs = 2000,
                Active = false
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("model:" + e.Id, ct => c.ModelEndpoints.DeleteAsync(e.Id, ct));
            Helpers.True(e.LastValidationSucceeded == null, "a new endpoint has never been checked");

            SetupStatusResponse afterKey = await StepAsync(context, result, "POST setup/model-key (unreachable endpoint)",
                () => c.Setup.SetModelKeyAsync(e.Id, "sk-harness-" + Helpers.Unique(), token)).ConfigureAwait(false);
            Helpers.True(afterKey.CanConfigure, "the administrator can configure");
            ModelEndpoint keyed = await StepAsync(context, result, "GET model endpoint (key attached)", () => c.ModelEndpoints.GetAsync(e.Id, token)).ConfigureAwait(false);
            string credentialId = Helpers.NotEmpty(keyed.CredentialId, "credential attached to the endpoint");
            context.Cleanup.Remove("model:" + e.Id);
            context.Cleanup.Add("model:" + e.Id, async ct =>
            {
                try
                {
                    await c.ModelEndpoints.DeleteAsync(e.Id, ct).ConfigureAwait(false);
                }
                catch (MyMorningNewsException ex) when (ex.StatusCode == 404)
                {
                }

                await c.Vault.DeleteAsync(credentialId, ct).ConfigureAwait(false);
            });
            Helpers.True(keyed.LastValidationSucceeded == false, "the failed live check is recorded");
            SetupCheck? check = afterKey.Checks.FirstOrDefault(x => x.ResourceId == e.Id);
            if (check != null) Helpers.True(check.State != SetupCheckStateEnum.Ready, "an unreachable endpoint is not Ready");

            string password = "Harness-" + Helpers.Unique();
            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, c, password, token)).ConfigureAwait(false);
            using (MyMorningNewsClient u = context.NewClient())
            {
                await StepAsync(context, result, "login as the plain user", () => u.LoginAsync(user.Email, password, context.Environment.Tenant, token)).ConfigureAwait(false);
                SetupStatusResponse mine = await StepAsync(context, result, "GET setup (user)", () => u.Setup.GetStatusAsync(token)).ConfigureAwait(false);
                Helpers.True(!mine.CanConfigure, "a plain user cannot configure");
                await StepAsync(context, result, "POST setup/verify as a user (403)", () => Helpers.ExpectErrorAsync(() => u.Setup.VerifyAsync(token), 403)).ConfigureAwait(false);
                await StepAsync(context, result, "POST setup/model-key as a user (403)", () => Helpers.ExpectErrorAsync(() => u.Setup.SetModelKeyAsync(e.Id, "sk-harness", token), 403)).ConfigureAwait(false);
            }

            await StepAsync(context, result, "DELETE user", () => Fixtures.DeleteUserAsync(context, c, user.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE model endpoint", () => c.ModelEndpoints.DeleteAsync(e.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE vault credential", () => c.Vault.DeleteAsync(credentialId, token)).ConfigureAwait(false);
            context.Cleanup.Remove("model:" + e.Id);
        }
    }
}
