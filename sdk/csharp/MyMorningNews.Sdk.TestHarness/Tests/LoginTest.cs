namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Email and password login, token details, validation, profile, and server info. Signs in the shared administrator client.
    /// </summary>
    public class LoginTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Token login, details, me, server info";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoginTest()
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
            MyMorningNewsClient client = context.NewClient();
            TokenResponse login = await StepAsync(context, result, "POST token (email and password)",
                () => client.LoginAsync(context.Environment.Email, context.Environment.Password, context.Environment.Tenant, token)).ConfigureAwait(false);
            Helpers.NotEmpty(login.Token, "session token");
            Helpers.Equal(login.Token, client.Token, "client stores the token");
            Helpers.Equal(context.Environment.Tenant, login.TenantId, "tenant");
            Helpers.NotEmpty(login.UserId, "user id");
            context.Admin = client;
            context.AdminUserId = login.UserId;

            TokenDetailsResponse details = await StepAsync(context, result, "GET token/details", () => client.GetTokenDetailsAsync(token)).ConfigureAwait(false);
            Helpers.True(details.Valid, "token is valid");
            Helpers.Equal(login.UserId, details.UserId, "details user");
            Helpers.True(details.ExpiresUtc.HasValue, "details carry the expiry");

            TokenDetailsResponse validated = await StepAsync(context, result, "GET token", () => client.ValidateTokenAsync(token)).ConfigureAwait(false);
            Helpers.True(validated.Valid, "validate");

            UserResponse me = await StepAsync(context, result, "GET me", () => client.GetMeAsync(token)).ConfigureAwait(false);
            Helpers.Equal(context.Environment.Email, me.Email, "me email");

            ServerInfoResponse info = await StepAsync(context, result, "GET server-info", () => client.GetServerInfoAsync(token)).ConfigureAwait(false);
            Helpers.NotEmpty(info.Version, "server info version");
        }
    }
}
