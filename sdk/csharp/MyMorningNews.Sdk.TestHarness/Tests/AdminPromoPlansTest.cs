namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Promotional codes with limits (create, read, update, list, public check, apply to a new user, redemptions, delete
    /// refused while used, delete with force) and plans and plan assignments (read).
    /// </summary>
    public class AdminPromoPlansTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Admin promo codes (limits, apply, force delete) and plans";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AdminPromoPlansTest()
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
            List<PlanResponse> plans = (await StepAsync(context, result, "GET admin/plans", () => c.Admin.Plans.ListAsync(options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.True(plans.Count > 0, "plans exist");

            await StepAsync(context, result, "POST promo code with a bad code (400)", () => Helpers.ExpectErrorAsync(() => c.Admin.PromoCodes.CreateAsync(new PromoCodeUpsertRequest { Code = "!" }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            string code = "SDKCS-" + Helpers.Unique().ToUpperInvariant();
            PromoCodeStats created = await StepAsync(context, result, "POST promo code (limits)", () => c.Admin.PromoCodes.CreateAsync(new PromoCodeUpsertRequest
            {
                Code = code,
                Description = context.Name("promo"),
                DurationDays = 7,
                MaxRedemptions = 3,
                PerUserLimit = 1,
                ExpiresUtc = DateTime.UtcNow.AddDays(30),
                Enabled = true
            }, token)).ConfigureAwait(false);
            string id = created.Code.Id;
            context.Cleanup.Add("promo:" + id, ct => c.Admin.PromoCodes.DeleteAsync(id, true, ct));
            Helpers.Equal(3, created.Code.MaxRedemptions ?? 0, "max redemptions");
            Helpers.Equal(3, created.Remaining ?? -1, "remaining");
            await StepAsync(context, result, "POST duplicate promo code (409)", () => Helpers.ExpectErrorAsync(() => c.Admin.PromoCodes.CreateAsync(new PromoCodeUpsertRequest { Code = code }, token), 409)).ConfigureAwait(false);

            PromoCodeStats read = await StepAsync(context, result, "GET promo code", () => c.Admin.PromoCodes.GetAsync(id, token)).ConfigureAwait(false);
            Helpers.Equal(code, read.Code.Code, "code");
            PromoCodeStats updated = await StepAsync(context, result, "PUT promo code", () => c.Admin.PromoCodes.UpdateAsync(id, new PromoCodeUpsertRequest { MaxRedemptions = 2 }, token)).ConfigureAwait(false);
            Helpers.Equal(2, updated.Code.MaxRedemptions ?? 0, "updated limit");
            EnumerationResult<PromoCodeStats> list = await StepAsync(context, result, "GET promo codes (active)", () => c.Admin.PromoCodes.ListAsync(new PromoCodeListOptions { Status = "active", Search = code }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Objects, s => s.Code.Id == id, "the code");
            PromoCheck check = await StepAsync(context, result, "GET public promo-code check", () => c.Account.CheckPromoCodeAsync(code, token)).ConfigureAwait(false);
            Helpers.True(check.Valid, "the code checks as valid");

            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, c, "Harness-" + Helpers.Unique(), token)).ConfigureAwait(false);
            PromoRedemption redemption = await StepAsync(context, result, "POST apply promo code to the user", () => c.Admin.PromoCodes.ApplyAsync(id, new PromoApplyRequest { TenantId = tenant, UserId = user.Id }, token)).ConfigureAwait(false);
            Helpers.Equal(user.Id, redemption.UserId, "redeemed for the user");
            await StepAsync(context, result, "POST apply again (per-user limit, 409)", () => Helpers.ExpectErrorAsync(() => c.Admin.PromoCodes.ApplyAsync(id, new PromoApplyRequest { TenantId = tenant, UserId = user.Id }, token), 409, "PromoCodeInvalid")).ConfigureAwait(false);
            List<PromoRedemption> redemptions = (await StepAsync(context, result, "GET redemptions", () => c.Admin.PromoCodes.ListRedemptionsAsync(id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.Contains(redemptions, r => r.Id == redemption.Id, "the redemption");
            List<PlanAssignment> assignments = (await StepAsync(context, result, "GET plan-assignments for the user", () => c.Admin.PlanAssignments.ListAsync(tenant, user.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.Contains(assignments, a => a.Id == redemption.PlanAssignmentId, "the promo plan assignment");

            await StepAsync(context, result, "DELETE used promo code without force (409)", () => Helpers.ExpectErrorAsync(() => c.Admin.PromoCodes.DeleteAsync(id, false, token), 409, "Conflict")).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE promo code with force", () => c.Admin.PromoCodes.DeleteAsync(id, true, token)).ConfigureAwait(false);
            context.Cleanup.Remove("promo:" + id);
            await StepAsync(context, result, "GET deleted promo code (404)", () => Helpers.ExpectErrorAsync(() => c.Admin.PromoCodes.GetAsync(id, token), 404)).ConfigureAwait(false);
            List<PlanAssignment> after = (await StepAsync(context, result, "GET plan-assignments after force delete", () => c.Admin.PlanAssignments.ListAsync(tenant, user.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.True(after.Where(a => a.Id == redemption.PlanAssignmentId).All(a => a.RevokedUtc != null), "force delete revokes the plan assignment");
            await StepAsync(context, result, "DELETE user", () => Fixtures.DeleteUserAsync(context, c, user.Id, token)).ConfigureAwait(false);

            using (MyMorningNewsClient anon = context.NewClient())
            {
                await StepAsync(context, result, "GET admin/plans without credentials (401)", () => Helpers.ExpectErrorAsync(() => anon.Admin.Plans.ListAsync(options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token), 401)).ConfigureAwait(false);
            }
        }
    }
}
