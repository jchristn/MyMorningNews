namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Plans and promotional codes (plan section 33.2): codes that give a free subscription, total and per-user limits,
    /// dates, bulk generation, usage tracking, revocation, plans and grants, and admin-only access.
    /// </summary>
    public static class PromoSuite
    {
        private const string S = "Promo";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Plans and promotional codes", new[]
            {
                Cases.Case(S, "Redeem", "A code gives a free subscription, counts usage, and stops at its limits", RedeemAsync),
                Cases.Case(S, "Concurrency", "Ten people racing for a five-use code get exactly five redemptions", ConcurrencyAsync),
                Cases.Case(S, "Rules", "Disabled, future, expired, sign-up-only, and malformed codes are refused with a reason", RulesAsync),
                Cases.Case(S, "AdminManage", "Administrators generate, edit, delete, revoke, and apply codes; nobody else can", AdminAsync),
                Cases.Case(S, "Plans", "Plans have editable values, can be granted and revoked, and codes stack", PlansAsync)
            });
        }

        private static async Task<PromoCodeStats> CreateCodeAsync(TestApiClient admin, object body, CancellationToken token)
        {
            return (await admin.PostAsync("/v1.0/api/admin/promo-codes", body, token).ConfigureAwait(false)).Expect(201, "create code").Json<PromoCodeStats>();
        }

        private static string Unique(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static async Task RedeemAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser a = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser b = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser c = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            string code = Unique("SPRING");
            PromoCodeStats created = await CreateCodeAsync(admin, new { code = code.ToLowerInvariant(), description = "Spring launch", durationDays = 30, maxRedemptions = 2 }, token).ConfigureAwait(false);
            Assert.True(created.Code.Code == code && created.PlanName == "Complimentary" && created.Remaining == 2 && created.Code.PerUserLimit == 1, "stored uppercase on the complimentary plan");

            TestApiClient anon = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
            PromoCheck check = (await anon.GetAsync("/v1.0/public/promo-codes/" + code, token).ConfigureAwait(false)).Expect(200, "check").Json<PromoCheck>();
            Assert.True(check.Valid && check.PlanName == "Complimentary" && check.DurationDays == 30 && check.Message.Contains("30 days", StringComparison.Ordinal), "public check");
            Assert.Equal("NotFound", (await anon.GetAsync("/v1.0/public/promo-codes/NOPE-0000", token).ConfigureAwait(false)).Json<PromoCheck>().Reason, "unknown code");

            EffectivePlan before = (await a.Client.GetAsync("/v1.0/api/account/plan", token).ConfigureAwait(false)).Expect(200, "plan").Json<EffectivePlan>();
            Assert.True(before.Plan.Key == "unlimited" && before.Assignment == null, "self-hosted default plan");
            EffectivePlan after = (await a.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = " " + code.ToLowerInvariant() + " " }, token).ConfigureAwait(false)).Expect(201, "redeem").Json<EffectivePlan>();
            Assert.True(after.Plan.Key == "complimentary" && after.Assignment!.Source == PlanSourceEnum.Promo && after.EndsUtc > DateTime.UtcNow.AddDays(29.9) && after.Features["topics.max"] == "25", "free subscription for 30 days");
            TestResponse again = (await a.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = code }, token).ConfigureAwait(false)).Expect(409, "once per user");
            Assert.True(again.Body.Contains("AlreadyRedeemed", StringComparison.Ordinal), "reason: " + again.Body);
            (await b.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = code }, token).ConfigureAwait(false)).Expect(201, "second person");
            TestResponse full = (await c.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = code }, token).ConfigureAwait(false)).Expect(409, "limit reached");
            Assert.True(full.Body.Contains("Exhausted", StringComparison.Ordinal), "exhausted");

            PromoCodeStats stats = (await admin.GetAsync("/v1.0/api/admin/promo-codes/" + created.Code.Id, token).ConfigureAwait(false)).Expect(200, "stats").Json<PromoCodeStats>();
            Assert.True(stats.Code.RedemptionCount == 2 && stats.Remaining == 0 && stats.ActiveAssignments == 2 && stats.LastRedeemedUtc.HasValue, "usage tracked");
            List<PromoRedemption> who = (await admin.GetAsync("/v1.0/api/admin/promo-codes/" + created.Code.Id + "/redemptions", token).ConfigureAwait(false)).Expect(200, "redemptions").Json<List<PromoRedemption>>();
            Assert.True(who.Count == 2 && who.Any(r => r.UserId == a.User.Id && r.Email == a.User.Email && r.Channel == PromoChannelEnum.Account), "who redeemed");
        }

        private static async Task ConcurrencyAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            List<TestUser> users = new List<TestUser>();
            for (int i = 0; i < 10; i++) users.Add(await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false));
            string code = Unique("RACE");
            PromoCodeStats created = await CreateCodeAsync(admin, new { code = code, durationDays = 7, maxRedemptions = 5 }, token).ConfigureAwait(false);
            TestResponse[] results = await Task.WhenAll(users.Select(u => u.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = code }, token))).ConfigureAwait(false);
            Assert.True(results.Count(r => r.StatusCode == 201) == 5 && results.Count(r => r.StatusCode == 409) == 5, "five succeed: " + String.Join(",", results.Select(r => r.StatusCode)));
            PromoCodeStats stats = (await admin.GetAsync("/v1.0/api/admin/promo-codes/" + created.Code.Id, token).ConfigureAwait(false)).Json<PromoCodeStats>();
            Assert.True(stats.Code.RedemptionCount == 5 && stats.ActiveAssignments == 5, "count matches redemptions");
        }

        private static async Task RulesAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            async Task<string> ReasonAsync(object body)
            {
                PromoCodeStats pc = await CreateCodeAsync(admin, body, token).ConfigureAwait(false);
                TestResponse r = (await u.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = pc.Code.Code }, token).ConfigureAwait(false)).Expect(409, "refused");
                return r.Body;
            }

            Assert.True((await ReasonAsync(new { code = Unique("OFF"), enabled = false }).ConfigureAwait(false)).Contains("Disabled", StringComparison.Ordinal), "disabled");
            Assert.True((await ReasonAsync(new { code = Unique("SOON"), startsUtc = DateTime.UtcNow.AddDays(2) }).ConfigureAwait(false)).Contains("NotStarted", StringComparison.Ordinal), "not started");
            Assert.True((await ReasonAsync(new { code = Unique("NEW"), newAccountsOnly = true }).ConfigureAwait(false)).Contains("NewAccountsOnly", StringComparison.Ordinal), "sign-up only");
            PromoCodeStats old = await CreateCodeAsync(admin, new { code = Unique("OLD"), expiresUtc = DateTime.UtcNow.AddDays(1) }, token).ConfigureAwait(false);
            PromoCode row = (await (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Database.PromoCodes.ReadAsync(old.Code.Id, token).ConfigureAwait(false))!;
            row.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            await (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Database.PromoCodes.UpdateAsync(row, token).ConfigureAwait(false);
            Assert.True((await u.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = old.Code.Code }, token).ConfigureAwait(false)).Body.Contains("Expired", StringComparison.Ordinal), "expired");
            (await admin.PostAsync("/v1.0/api/admin/promo-codes", new { code = "a b" }, token).ConfigureAwait(false)).Expect(400, "malformed");
            (await admin.PostAsync("/v1.0/api/admin/promo-codes", new { code = old.Code.Code }, token).ConfigureAwait(false)).Expect(409, "duplicate");
            (await admin.PostAsync("/v1.0/api/admin/promo-codes", new { code = Unique("BAD"), startsUtc = DateTime.UtcNow.AddDays(5), expiresUtc = DateTime.UtcNow.AddDays(1) }, token).ConfigureAwait(false)).Expect(400, "dates in order");
        }

        private static async Task AdminAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            (await member.Client.GetAsync("/v1.0/api/admin/promo-codes", token).ConfigureAwait(false)).Expect(403, "members cannot see codes");
            (await ta.Client.PostAsync("/v1.0/api/admin/promo-codes", new { code = Unique("TA") }, token).ConfigureAwait(false)).Expect(403, "tenant administrators cannot create codes");
            (await ta.Client.GetAsync("/v1.0/api/admin/plans", token).ConfigureAwait(false)).Expect(403, "or see plans");

            List<PromoCode> batch = (await admin.PostAsync("/v1.0/api/admin/promo-codes/generate", new { prefix = "vip", count = 3, durationDays = 90, description = "Partner batch" }, token).ConfigureAwait(false))
                .Expect(201, "generate").Json<List<PromoCode>>();
            Assert.True(batch.Count == 3 && batch.All(c => c.Code.StartsWith("VIP-", StringComparison.Ordinal) && c.Code.Length == 12 && c.MaxRedemptions == 1 && c.DurationDays == 90 && c.BatchId == batch[0].BatchId) && batch.Select(c => c.Code).Distinct().Count() == 3,
                "three single-use codes in one batch");
            EnumerationResult<PromoCodeStats> listed = (await admin.GetAsync("/v1.0/api/admin/promo-codes?batchId=" + batch[0].BatchId, token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<PromoCodeStats>>();
            Assert.Equal(3, listed.Items.Count, "list by batch");

            PromoRedemption applied = (await admin.PostAsync("/v1.0/api/admin/promo-codes/" + batch[0].Id + "/apply", new { tenantId = t.Id, userId = member.User.Id }, token).ConfigureAwait(false)).Expect(201, "apply").Json<PromoRedemption>();
            Assert.Equal(PromoChannelEnum.Admin, applied.Channel, "applied by an administrator");
            Assert.Equal("complimentary", (await member.Client.GetAsync("/v1.0/api/account/plan", token).ConfigureAwait(false)).Json<EffectivePlan>().Plan.Key, "member has the plan");
            (await admin.DeleteAsync("/v1.0/api/admin/promo-codes/" + batch[0].Id, token).ConfigureAwait(false)).Expect(409, "used codes are disabled, not deleted");
            (await admin.PutAsync("/v1.0/api/admin/promo-codes/" + batch[0].Id, new { maxRedemptions = 0, enabled = false }, token).ConfigureAwait(false)).Expect(200, "unlimited and off");
            PromoRedemption revoked = (await admin.PostAsync("/v1.0/api/admin/promo-codes/" + batch[0].Id + "/redemptions/" + applied.Id + "/revoke", null, token).ConfigureAwait(false)).Expect(200, "revoke").Json<PromoRedemption>();
            Assert.True(revoked.RevokedUtc.HasValue, "revoked");
            Assert.Equal("unlimited", (await member.Client.GetAsync("/v1.0/api/account/plan", token).ConfigureAwait(false)).Json<EffectivePlan>().Plan.Key, "back to the default plan");
            PromoCodeStats stats = (await admin.GetAsync("/v1.0/api/admin/promo-codes/" + batch[0].Id, token).ConfigureAwait(false)).Json<PromoCodeStats>();
            Assert.True(stats.Revoked == 1 && stats.ActiveAssignments == 0 && stats.Code.RedemptionCount == 1 && stats.Remaining == null && !stats.Code.Enabled, "usage after revocation");
            (await admin.PutAsync("/v1.0/api/admin/promo-codes/" + batch[1].Id, new { code = "RENAMED" }, token).ConfigureAwait(false)).Expect(400, "codes cannot be renamed");
            (await admin.DeleteAsync("/v1.0/api/admin/promo-codes/" + batch[1].Id, token).ConfigureAwait(false)).Expect(204, "delete unused");
        }

        private static async Task PlansAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            List<PlanResponse> plans = (await admin.GetAsync("/v1.0/api/admin/plans", token).ConfigureAwait(false)).Expect(200, "plans").Json<List<PlanResponse>>();
            Assert.True(plans.Select(p => p.Plan.Key).Take(3).SequenceEqual(new[] { "free", "complimentary", "unlimited" }) && plans.Single(p => p.Plan.Key == "unlimited").Plan.IsDefault && plans.Single(p => p.Plan.Key == "free").Features["topics.max"] == "5",
                "built-in plans with values");
            string key = "partner-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            PlanResponse partner = (await admin.PostAsync("/v1.0/api/admin/plans", new { key = key, name = "Partner", features = new Dictionary<string, string> { { "topics.max", "12" }, { "audio.enabled", "false" } } }, token).ConfigureAwait(false))
                .Expect(201, "create plan").Json<PlanResponse>();
            PlanResponse edited = (await admin.PutAsync("/v1.0/api/admin/plans/" + partner.Plan.Id, new { features = new Dictionary<string, string> { { "topics.max", "15" }, { "audio.enabled", "" } } }, token).ConfigureAwait(false))
                .Expect(200, "edit plan").Json<PlanResponse>();
            Assert.True(edited.Features["topics.max"] == "15" && !edited.Features.ContainsKey("audio.enabled"), "values set and removed");

            PlanAssignment grant = (await admin.PostAsync("/v1.0/api/admin/plan-assignments", new { tenantId = t.Id, userId = u.User.Id, planId = partner.Plan.Id, durationDays = 7, note = "Pilot" }, token).ConfigureAwait(false))
                .Expect(201, "grant").Json<PlanAssignment>();
            EffectivePlan mine = (await u.Client.GetAsync("/v1.0/api/account/plan", token).ConfigureAwait(false)).Json<EffectivePlan>();
            Assert.True(mine.Plan.Key == key && mine.Features["topics.max"] == "15" && mine.Assignment!.Source == PlanSourceEnum.Admin, "granted plan in effect");
            (await admin.DeleteAsync("/v1.0/api/admin/plan-assignments/" + grant.Id + "?tenantId=" + t.Id, token).ConfigureAwait(false)).Expect(204, "revoke grant");
            Assert.Equal("unlimited", (await u.Client.GetAsync("/v1.0/api/account/plan", token).ConfigureAwait(false)).Json<EffectivePlan>().Plan.Key, "default again");

            PromoCodeStats one = await CreateCodeAsync(admin, new { code = Unique("FIRST"), durationDays = 10 }, token).ConfigureAwait(false);
            PromoCodeStats two = await CreateCodeAsync(admin, new { code = Unique("SECOND"), durationDays = 20 }, token).ConfigureAwait(false);
            await u.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = one.Code.Code }, token).ConfigureAwait(false);
            EffectivePlan stacked = (await u.Client.PostAsync("/v1.0/api/promo-codes/redeem", new { code = two.Code.Code }, token).ConfigureAwait(false)).Expect(201, "second code").Json<EffectivePlan>();
            Assert.True(stacked.Upcoming.Count == 1 && Math.Abs((stacked.Upcoming[0].StartsUtc - stacked.EndsUtc!.Value).TotalSeconds) < 2 && Math.Abs((stacked.Upcoming[0].EndsUtc!.Value - stacked.EndsUtc.Value).TotalDays - 20) < 0.01,
                "the second code starts when the first ends");
            List<PlanAssignment> history = (await admin.GetAsync("/v1.0/api/admin/plan-assignments?tenantId=" + t.Id + "&userId=" + u.User.Id, token).ConfigureAwait(false)).Expect(200, "history").Json<List<PlanAssignment>>();
            Assert.Equal(3, history.Count, "every assignment kept");
        }
    }
}
