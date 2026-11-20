namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Typed errors for a bad password and for an unauthenticated call, and logout revoking a session.
    /// </summary>
    public class AuthErrorTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Bad login, unauthenticated call, logout";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthErrorTest()
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
            using (MyMorningNewsClient client = context.NewClient())
            {
                MyMorningNewsException bad = await StepAsync(context, result, "POST token with a wrong password (401)",
                    () => Helpers.ExpectErrorAsync(() => client.LoginAsync(context.Environment.Email, "not-the-password-" + Helpers.Unique(), context.Environment.Tenant, token), 401)).ConfigureAwait(false);
                Helpers.NotEmpty(bad.ErrorCode, "error code");
                Helpers.True(client.Token == null, "a failed login stores no token");

                MyMorningNewsException anon = await StepAsync(context, result, "GET me without credentials (401)",
                    () => Helpers.ExpectErrorAsync(() => client.GetMeAsync(token), 401)).ConfigureAwait(false);
                Helpers.NotEmpty(anon.RequestId, "request id on the error");

                await StepAsync(context, result, "POST token", () => client.LoginAsync(context.Environment.Email, context.Environment.Password, context.Environment.Tenant, token)).ConfigureAwait(false);
                string? revoked = client.Token;
                await StepAsync(context, result, "DELETE token (logout)", () => client.LogoutAsync(token)).ConfigureAwait(false);
                Helpers.True(client.Token == null, "logout clears the token");
                client.Token = revoked;
                await StepAsync(context, result, "GET me with the revoked token (401)", () => Helpers.ExpectErrorAsync(() => client.GetMeAsync(token), 401)).ConfigureAwait(false);
                client.Token = null;
            }
        }
    }
}
