namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Account: the caller's plan, the public promotional code check, and a typed 409 for an unknown code.
    /// </summary>
    public class AccountTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Account plan, promo check, redeem invalid code";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AccountTest()
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
            EffectivePlan plan = await StepAsync(context, result, "GET account/plan", () => c.Account.GetPlanAsync(token)).ConfigureAwait(false);
            Helpers.NotEmpty(plan.Plan.Key, "plan key");
            string code = "NOPE-" + Helpers.Unique().ToUpperInvariant();
            PromoCheck check = await StepAsync(context, result, "GET public promo-code check", () => c.Account.CheckPromoCodeAsync(code, token)).ConfigureAwait(false);
            Helpers.True(!check.Valid, "an unknown code is not valid");
            MyMorningNewsException ex = await StepAsync(context, result, "POST promo-codes/redeem unknown (409)", () => Helpers.ExpectErrorAsync(() => c.Account.RedeemAsync(code, token), 409, "PromoCodeInvalid")).ConfigureAwait(false);
            Helpers.True(ex.Details.ContainsKey("reason"), "details carry the reason");
        }
    }
}
