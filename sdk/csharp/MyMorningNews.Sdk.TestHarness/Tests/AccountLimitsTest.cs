namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Account usage, export, deletion, and plan limits: a new user on the Free plan reads the usage meter, downloads the
    /// data export (ZIP), is refused a second topic with a typed CapReachedException (402 with key, limit), schedules account
    /// deletion, and signing in again keeps the account.
    /// </summary>
    public class AccountLimitsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Account usage, export, delete, and 402 CapReached on the Free plan";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AccountLimitsTest()
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
            UsageMeter adminMeter = await StepAsync(context, result, "GET account/usage (admin)", () => c.Account.GetUsageAsync(token)).ConfigureAwait(false);
            Helpers.NotEmpty(adminMeter.PlanKey, "plan key");
            Helpers.True(adminMeter.ResetUtc > DateTime.UtcNow.AddDays(-1), "reset time is in the future");
            MyMorningNewsException protectedEx = await StepAsync(context, result, "POST account/delete as the administrator (409)", () => Helpers.ExpectErrorAsync(() => c.Account.DeleteAsync(token), 409, "Conflict")).ConfigureAwait(false);
            Helpers.NotEmpty(protectedEx.Message, "message");

            List<PlanResponse> plans = (await StepAsync(context, result, "GET admin/plans", () => c.Admin.Plans.ListAsync(options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            PlanResponse? free = plans.FirstOrDefault(p => p.Plan.Key == "free");
            if (free == null) throw new SkipTestException("the server has no Free plan");

            string password = "Harness-" + Helpers.Unique();
            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, c, password, token)).ConfigureAwait(false);
            PlanAssignment grant = await StepAsync(context, result, "POST plan-assignment (free)", () => c.Admin.PlanAssignments.CreateAsync(new PlanGrantRequest
            {
                TenantId = tenant,
                UserId = user.Id,
                PlanId = free.Plan.Id,
                DurationDays = 1,
                Note = "SDK C# harness"
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("plan-assignment:" + grant.Id, ct => c.Admin.PlanAssignments.DeleteAsync(grant.Id, tenant, ct));

            using (MyMorningNewsClient u = context.NewClient())
            {
                await StepAsync(context, result, "login as the Free user", () => u.LoginAsync(user.Email, password, tenant, token)).ConfigureAwait(false);
                UsageMeter meter = await StepAsync(context, result, "GET account/usage (free)", () => u.Account.GetUsageAsync(token)).ConfigureAwait(false);
                Helpers.Equal("free", meter.PlanKey, "plan key");
                Helpers.True(meter.TopicsMax.HasValue, "the Free plan limits topics");
                long max = meter.TopicsMax ?? 0;

                DownloadResult zip = await StepAsync(context, result, "GET account/export", () => u.Account.ExportAsync(token)).ConfigureAwait(false);
                Helpers.Equal("application/zip", zip.ContentType, "content type");
                Helpers.True(zip.Data.Length > 4 && zip.Data[0] == (byte)'P' && zip.Data[1] == (byte)'K', "ZIP signature");

                List<string> created = new List<string>();
                try
                {
                    for (long i = meter.Topics; i < max; i++)
                    {
                        Topic t = await StepAsync(context, result, "POST topic within the limit", () => u.Topics.CreateAsync(new TopicUpsertRequest { Name = context.Name("cap"), Brief = "Harness data; plan limit check." }, token)).ConfigureAwait(false);
                        created.Add(t.Id);
                    }

                    MyMorningNewsException ex = await StepAsync(context, result, "POST topic over the limit (402 CapReached)", () => Helpers.ExpectErrorAsync(() => u.Topics.CreateAsync(new TopicUpsertRequest { Name = context.Name("cap"), Brief = "Over the limit." }, token), 402, "CapReached")).ConfigureAwait(false);
                    CapReachedException cap = ex as CapReachedException ?? throw new HarnessAssertionException("402 CapReached should be a CapReachedException, got " + ex.GetType().Name);
                    Helpers.Equal("topics.max", cap.Key, "cap key");
                    Helpers.Equal(max.ToString(System.Globalization.CultureInfo.InvariantCulture), cap.Limit, "cap limit");
                    Helpers.True(cap.Details.ContainsKey("key"), "details keep the raw key");
                }
                finally
                {
                    foreach (string id in created) await u.Topics.DeleteAsync(id, CancellationToken.None).ConfigureAwait(false);
                }

                AccountDeletionResponse deletion = await StepAsync(context, result, "POST account/delete (202)", () => u.Account.DeleteAsync(token)).ConfigureAwait(false);
                Helpers.True(deletion.PurgeAfterUtc > DateTime.UtcNow, "purge is scheduled in the future");
                Helpers.NotEmpty(deletion.Message, "message");
                await StepAsync(context, result, "the old session is signed out (401)", () => Helpers.ExpectErrorAsync(() => u.GetMeAsync(token), 401)).ConfigureAwait(false);
                await StepAsync(context, result, "login again cancels the deletion", () => u.LoginAsync(user.Email, password, tenant, token)).ConfigureAwait(false);
                UserResponse me = await StepAsync(context, result, "GET me after signing in again", () => u.GetMeAsync(token)).ConfigureAwait(false);
                Helpers.Equal(user.Id, me.Id, "same account");
                await u.LogoutAsync(token).ConfigureAwait(false);
            }

            await StepAsync(context, result, "DELETE plan-assignment", () => c.Admin.PlanAssignments.DeleteAsync(grant.Id, tenant, token)).ConfigureAwait(false);
            context.Cleanup.Remove("plan-assignment:" + grant.Id);
            await StepAsync(context, result, "DELETE user", () => Fixtures.DeleteUserAsync(context, c, user.Id, token)).ConfigureAwait(false);
        }
    }
}
