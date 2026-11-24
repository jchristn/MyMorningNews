namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Email: outbox list and read, preferences round trip, test email (queued, read, then deleted), and 404s.
    /// </summary>
    public class EmailTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Email outbox, test email, preferences";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EmailTest()
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
            EnumerationResult<EmailMessage> page = await StepAsync(context, result, "GET email-messages", () => c.Email.ListAsync(new EmailMessageListOptions { PageSize = 5 }, token)).ConfigureAwait(false);
            if (page.Objects.Count > 0)
            {
                Helpers.True(page.Objects[0].RenderedHtml == null, "lists leave rendered bodies out");
                EmailMessage m = await StepAsync(context, result, "GET email-message", () => c.Email.GetAsync(page.Objects[0].Id, token)).ConfigureAwait(false);
                Helpers.Equal(page.Objects[0].Id, m.Id, "message id");
            }

            await StepAsync(context, result, "GET unknown email-message (404)", () => Helpers.ExpectErrorAsync(() => c.Email.GetAsync("eml_missing", token), 404)).ConfigureAwait(false);

            EmailPreferencesResponse prefs = await StepAsync(context, result, "GET email-preferences", () => c.Email.GetPreferencesAsync(token)).ConfigureAwait(false);
            Helpers.NotEmpty(prefs.Address, "address");
            EmailPreferencesResponse flipped = await StepAsync(context, result, "PUT email-preferences (flip failure emails)",
                () => c.Email.UpdatePreferencesAsync(new EmailPreferencesRequest { FailureEmails = !prefs.FailureEmails }, token)).ConfigureAwait(false);
            Helpers.Equal(!prefs.FailureEmails, flipped.FailureEmails, "failure emails flipped");
            EmailPreferencesResponse restored = await StepAsync(context, result, "PUT email-preferences (restore)",
                () => c.Email.UpdatePreferencesAsync(new EmailPreferencesRequest { FailureEmails = prefs.FailureEmails }, token)).ConfigureAwait(false);
            Helpers.Equal(prefs.FailureEmails, restored.FailureEmails, "failure emails restored");

            EmailMessage test = await StepAsync(context, result, "POST email/test", () => c.Email.SendTestAsync(token)).ConfigureAwait(false);
            context.Cleanup.Add("email:" + test.Id, ct => c.Email.DeleteAsync(test.Id, ct));
            Helpers.Equal(prefs.Address, test.Recipient, "test email goes to the caller");
            EmailMessage read = await StepAsync(context, result, "GET test email", () => c.Email.GetAsync(test.Id, token)).ConfigureAwait(false);
            Helpers.NotEmpty(read.Subject, "subject");
            await StepAsync(context, result, "DELETE test email", () => c.Email.DeleteAsync(test.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("email:" + test.Id);
            await StepAsync(context, result, "GET deleted email (404)", () => Helpers.ExpectErrorAsync(() => c.Email.GetAsync(test.Id, token), 404)).ConfigureAwait(false);
        }
    }
}
