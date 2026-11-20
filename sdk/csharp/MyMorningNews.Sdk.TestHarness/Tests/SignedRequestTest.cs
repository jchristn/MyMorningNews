namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Signed requests (MMN1-HMAC-SHA256) with a new SignedRequest credential: a GET with a query and a POST with a body.
    /// Skipped when the server rejects the signed call with 401 (it predates signed requests).
    /// </summary>
    public class SignedRequestTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Signed requests (live, when the server supports them)";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SignedRequestTest()
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
            string tenant = context.Environment.Tenant;
            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, c, "Harness-" + Helpers.Unique(), token)).ConfigureAwait(false);
            CredentialCreatedResponse created = await StepAsync(context, result, "POST credential (SignedRequest)", () => c.Admin.Credentials.CreateAsync(tenant, new CredentialCreateRequest
            {
                UserId = user.Id,
                Name = context.Name("signed"),
                AuthMode = CredentialAuthModeEnum.SignedRequest
            }, token)).ConfigureAwait(false);
            string credId = created.Credential.Id;
            context.Cleanup.Add("credential:" + credId, ct => c.Admin.Credentials.DeleteAsync(tenant, credId, ct));

            try
            {
                using (MyMorningNewsClient signed = context.NewClient())
                {
                    signed.AccessKey = created.Credential.AccessKey;
                    signed.SecretKey = created.SecretKey;
                    signed.TenantId = tenant;
                    signed.UseSignedRequests = true;
                    TokenDetailsResponse details;
                    try
                    {
                        details = await signed.GetTokenDetailsAsync(token).ConfigureAwait(false);
                    }
                    catch (MyMorningNewsException ex) when (ex.StatusCode == 401)
                    {
                        throw new SkipTestException("The server does not accept signed requests yet (401: " + ex.Message + ").");
                    }

                    Helpers.Equal(credId, details.CredentialId, "signed call authenticates as the credential");
                    Helpers.Equal(AuthSchemeEnum.AccessKeySignature, details.Scheme ?? AuthSchemeEnum.PasswordHeaders, "signature scheme");
                    await StepAsync(context, result, "GET topics with a query (signed)", () => signed.Topics.ListAsync(new TopicListOptions { PageSize = 1 }, token)).ConfigureAwait(false);
                    await StepAsync(context, result, "POST schedules/preview with a body (signed)", () => signed.Schedules.PreviewAsync(new ScheduleUpsertRequest { IntervalType = IntervalTypeEnum.Manual }, token)).ConfigureAwait(false);
                    signed.SecretKey = "wrong-" + Helpers.Unique();
                    await StepAsync(context, result, "GET token/details signed with a wrong secret (401)", () => Helpers.ExpectErrorAsync(() => signed.GetTokenDetailsAsync(token), 401)).ConfigureAwait(false);
                }
            }
            finally
            {
                await c.Admin.Credentials.DeleteAsync(tenant, credId, token).ConfigureAwait(false);
                context.Cleanup.Remove("credential:" + credId);
                await Fixtures.DeleteUserAsync(context, c, user.Id, token).ConfigureAwait(false);
            }
        }
    }
}
