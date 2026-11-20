namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Audit records, request history (list, read, summary), and settings (read only).
    /// </summary>
    public class AdminAuditHistoryTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Admin audit, request history, settings";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AdminAuditHistoryTest()
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
            EnumerationResult<AuditRecord> audit = await StepAsync(context, result, "GET audit", () => c.Admin.Audit.ListAsync(new AuditListOptions { TenantId = context.Environment.Tenant, PageSize = 10 }, token)).ConfigureAwait(false);
            Helpers.True(audit.Items.Count > 0, "the harness produced audit records");
            AuditRecord record = await StepAsync(context, result, "GET audit record", () => c.Admin.Audit.GetAsync(audit.Items[0].Id, token)).ConfigureAwait(false);
            Helpers.Equal(audit.Items[0].Id, record.Id, "audit id");
            await StepAsync(context, result, "GET unknown audit record (404)", () => Helpers.ExpectErrorAsync(() => c.Admin.Audit.GetAsync("aud_missing", token), 404)).ConfigureAwait(false);

            EnumerationResult<RequestHistoryEntry> history = await StepAsync(context, result, "GET request-history (2xx, topics)",
                () => c.Admin.RequestHistory.ListAsync(new RequestHistoryListOptions { StatusClass = "2xx", PathContains = "/topics", PageSize = 5 }, token)).ConfigureAwait(false);
            if (history.Items.Count > 0)
            {
                RequestHistoryEntry entry = await StepAsync(context, result, "GET request-history entry", () => c.Admin.RequestHistory.GetAsync(history.Items[0].Id, token)).ConfigureAwait(false);
                Helpers.Equal(history.Items[0].Id, entry.Id, "history id");
            }

            RequestHistorySummary summary = await StepAsync(context, result, "GET request-history/summary", () => c.Admin.RequestHistory.GetSummaryAsync(new RequestHistoryListOptions
            {
                FromUtc = DateTime.UtcNow.AddHours(-1),
                ToUtc = DateTime.UtcNow,
                BucketMinutes = 15
            }, token)).ConfigureAwait(false);
            Helpers.True(summary.Buckets.Count >= 4, "summary buckets");
            await StepAsync(context, result, "GET request-history/summary without a window (400)", () => Helpers.ExpectErrorAsync(() => c.Admin.RequestHistory.GetSummaryAsync(new RequestHistoryListOptions(), token), 400)).ConfigureAwait(false);

            SettingsResponse settings = await StepAsync(context, result, "GET settings", () => c.Admin.Settings.GetAsync(token)).ConfigureAwait(false);
            Helpers.True(settings.Settings.Count > 0 && settings.Fields.Count > 0, "settings and field metadata");
        }
    }
}
