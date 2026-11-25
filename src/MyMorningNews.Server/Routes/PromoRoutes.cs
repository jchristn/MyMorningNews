namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Plans and promotional codes (plan section 33.2). Global administrators create codes that give a plan (a free
    /// subscription) for a number of days, set total and per-user limits and dates, generate codes in bulk, watch usage,
    /// and revoke redemptions; they also manage plans and grant plans directly. Signed-in users redeem codes and read
    /// their plan. A rate-limited public route checks a code for the sign-up page.
    /// </summary>
    public class PromoRoutes : RouteBase
    {
        private readonly LoginRateLimiter _Limiter;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public PromoRoutes(ServerContext server) : base(server)
        {
            _Limiter = new LoginRateLimiter(server.Settings.Hosting.PromoChecksPerMinute);
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string a = "/v1.0/api/admin/promo-codes";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, a, ListAsync, null, openApiMetadata: Paged(Doc("List promotional codes with usage", "Promotional codes", "Global administrators only. status: active, disabled, notstarted, expired, or exhausted.")
                .WithParameter(QueryParam("status", typeof(string)))
                .WithParameter(QueryParam("enabled", typeof(bool))).WithParameter(QueryParam("planId", typeof(string))).WithParameter(QueryParam("batchId", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, a + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List promotional codes with usage", "Promotional codes", "Global administrators only. status: active, disabled, notstarted, expired, or exhausted.")
                .WithParameter(QueryParam("status", typeof(string)))
                .WithParameter(QueryParam("enabled", typeof(bool))).WithParameter(QueryParam("planId", typeof(string))).WithParameter(QueryParam("batchId", typeof(string)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, a, CreateAsync, null, openApiMetadata: Doc("Create a promotional code", "Promotional codes").WithRequestBody(Body<PromoCodeUpsertRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, a + "/generate", GenerateAsync, null, openApiMetadata: Doc("Generate promotional codes in bulk", "Promotional codes").WithRequestBody(Body<PromoCodeGenerateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, a + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a promotional code with usage", "Promotional codes"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, a + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update a promotional code (limits, dates, plan, on or off)", "Promotional codes").WithRequestBody(Body<PromoCodeUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, a + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an unused promotional code (disable a used one instead)", "Promotional codes",
                "force=true also revokes and removes its redemptions (for test codes); the plan assignments stay, revoked, for history.").WithParameter(QueryParam("force", typeof(bool))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, a + "/{id}/redemptions", RedemptionsAsync, null, openApiMetadata: Paged(Doc("Who redeemed a code and when", "Promotional codes")));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, a + "/{id}/redemptions" + "/enumerate", ctx => EnumerateAsync(ctx, RedemptionsAsync), null, openApiMetadata: Paged(Doc("Who redeemed a code and when", "Promotional codes")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, a + "/{id}/redemptions/{redemptionId}/revoke", RevokeAsync, null, openApiMetadata: Doc("Revoke a redemption (ends its plan now)", "Promotional codes"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, a + "/{id}/apply", ApplyAsync, null, openApiMetadata: Doc("Redeem a code for a user", "Promotional codes").WithRequestBody(Body<PromoApplyRequest>()));

            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/admin/plans", ListPlansAsync, null, openApiMetadata: Paged(Doc("List plans with entitlement values", "Plans")));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/admin/plans" + "/enumerate", ctx => EnumerateAsync(ctx, ListPlansAsync), null, openApiMetadata: Paged(Doc("List plans with entitlement values", "Plans")).WithRequestBody(Body<EnumerationQuery>()));
            const string userLimits = "/v1.0/tenants/{tenantId}/users/{userId}/entitlements";
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, userLimits, UserEntitlementsAsync, null, openApiMetadata: Doc("Read a user's effective plan and overrides", "Plans", "Global administrators only."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, userLimits, SetOverrideAsync, null, openApiMetadata: Doc("Set one user entitlement override", "Plans", "Global administrators only. Zero is a real limit; unlimited removes the quantity cap.").WithRequestBody(Body<EntitlementOverrideRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, userLimits + "/{featureKey}", DeleteOverrideAsync, null, openApiMetadata: Doc("Restore a user entitlement to its plan default", "Plans", "Global administrators only."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/admin/plans", CreatePlanAsync, null, openApiMetadata: Doc("Create a plan", "Plans").WithRequestBody(Body<PlanUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/admin/plans/{id}", UpdatePlanAsync, null, openApiMetadata: Doc("Update a plan and its values", "Plans").WithRequestBody(Body<PlanUpsertRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/admin/plan-assignments", ListAssignmentsAsync, null, openApiMetadata: Paged(Doc("A user's plan assignments", "Plans")
                .WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/admin/plan-assignments" + "/enumerate", ctx => EnumerateAsync(ctx, ListAssignmentsAsync), null, openApiMetadata: Paged(Doc("A user's plan assignments", "Plans")
                .WithParameter(QueryParam("tenantId", typeof(string))).WithParameter(QueryParam("userId", typeof(string)))).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/admin/plan-assignments", GrantAsync, null, openApiMetadata: Doc("Give a user a plan", "Plans", "replaceCurrent=true revokes the user's live administrator assignments first, so the new plan replaces them.").WithRequestBody(Body<PlanGrantRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/admin/plan-assignments/{id}", RevokeAssignmentAsync, null, openApiMetadata: Doc("Revoke a plan assignment", "Plans")
                .WithParameter(QueryParam("tenantId", typeof(string))));

            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/promo-codes/redeem", RedeemAsync, null, openApiMetadata: Doc("Redeem a promotional code", "Account",
                "409 PromoCodeInvalid with details.reason: NotFound, Disabled, NotStarted, Expired, Exhausted, AlreadyRedeemed, or NewAccountsOnly.").WithRequestBody(Body<PromoRedeemRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/account/plan", MyPlanAsync, null, openApiMetadata: Doc("My plan and its limits", "Account"));
            webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/public/promo-codes/{code}", CheckAsync, null, openApiMetadata: Doc("Check a promotional code (sign-up page)", "Public"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PromoCodeFilter filter = new PromoCodeFilter { Enabled = QueryBool(ctx, "enabled"), PlanId = Query(ctx, "planId"), BatchId = Query(ctx, "batchId"), Search = Query(ctx, "search") };
            ApplyPaging(ctx, filter);
            filter.Status = Query(ctx, "status")?.Trim().ToLowerInvariant();
            EnumerationResult<PromoCode> page = await Server.Database.PromoCodes.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            List<PromoCodeStats> stats = new List<PromoCodeStats>();
            foreach (PromoCode pc in page.Objects) stats.Add(await Server.PromoCodes.StatsAsync(pc, ctx.Token).ConfigureAwait(false));
            await SendJsonAsync(ctx, 200, new EnumerationResult<PromoCodeStats> { Objects = stats, PageNumber = page.PageNumber, PageSize = page.PageSize, TotalRecords = page.TotalRecords, TotalPages = page.TotalPages, TotalMs = page.TotalMs }).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PromoCodeUpsertRequest? req = await ReadBodyAsync<PromoCodeUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string code = PromoCodeService.Normalize(req.Code);
            if (!PromoCodeService.IsWellFormed(code)) { await SendErrorAsync(ctx, 400, "ValidationFailed", "code must be 4 to 40 letters, digits, and inner dashes.").ConfigureAwait(false); return; }
            if (await Server.Database.PromoCodes.ReadByCodeAsync(code, ctx.Token).ConfigureAwait(false) != null) { await SendErrorAsync(ctx, 409, "Conflict", "That code already exists.").ConfigureAwait(false); return; }
            PromoCode pc = new PromoCode { Code = code, CreatedByUserId = Context(ctx).UserId };
            if (!await ApplyAsync(ctx, pc, req.Description, req.PlanId, req.DurationDays, req.MaxRedemptions, req.PerUserLimit, req.StartsUtc, req.ExpiresUtc, req.Enabled, req.NewAccountsOnly, true).ConfigureAwait(false)) return;
            if (!await ApplyOfferAsync(ctx, pc, req.BenefitKind, req.BillingPlanKey, req.DiscountPercent, req.DiscountMonths, req.AppleOfferId, req.GoogleOfferId).ConfigureAwait(false)) return;
            if (pc.BenefitKind == "PaidOffer" && req.DurationDays == null) pc.DurationDays = null;
            pc = await Server.Database.PromoCodes.CreateAsync(pc, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeChanged, 201, "Promotional code " + pc.Code + " created.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await Server.PromoCodes.StatsAsync(pc, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task GenerateAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PromoCodeGenerateRequest? req = await ReadBodyAsync<PromoCodeGenerateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (req.Count < 1 || req.Count > 1000) { await SendErrorAsync(ctx, 400, "ValidationFailed", "count must be 1 to 1000.").ConfigureAwait(false); return; }
            PromoCode template = new PromoCode { Code = "TEMPLATE", CreatedByUserId = Context(ctx).UserId, MaxRedemptions = 1 };
            if (!await ApplyAsync(ctx, template, req.Description, req.PlanId, req.DurationDays, req.MaxRedemptions, req.PerUserLimit, req.StartsUtc, req.ExpiresUtc, req.Enabled, req.NewAccountsOnly, true).ConfigureAwait(false)) return;
            if (!await ApplyOfferAsync(ctx, template, req.BenefitKind, req.BillingPlanKey, req.DiscountPercent, req.DiscountMonths, req.AppleOfferId, req.GoogleOfferId).ConfigureAwait(false)) return;
            if (template.BenefitKind == "PaidOffer" && req.DurationDays == null) template.DurationDays = null;
            List<PromoCode> created = await Server.PromoCodes.GenerateAsync(template, req.Prefix, req.Count, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeChanged, 201, created.Count + " promotional codes generated in " + created.FirstOrDefault()?.BatchId + ".").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, created).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            PromoCode? pc = await CodeAsync(ctx).ConfigureAwait(false);
            if (pc == null) return;
            await SendJsonAsync(ctx, 200, await Server.PromoCodes.StatsAsync(pc, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            PromoCode? pc = await CodeAsync(ctx).ConfigureAwait(false);
            if (pc == null) return;
            PromoCodeUpsertRequest? req = await ReadBodyAsync<PromoCodeUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (req.Code != null && PromoCodeService.Normalize(req.Code) != pc.Code) { await SendErrorAsync(ctx, 400, "ValidationFailed", "A code cannot be renamed; create a new one.").ConfigureAwait(false); return; }
            if (req.ClearStartsUtc == true) pc.StartsUtc = null;
            if (req.ClearExpiresUtc == true) pc.ExpiresUtc = null;
            if (!await ApplyAsync(ctx, pc, req.Description, req.PlanId, req.DurationDays, req.MaxRedemptions, req.PerUserLimit, req.StartsUtc, req.ExpiresUtc, req.Enabled, req.NewAccountsOnly, false).ConfigureAwait(false)) return;
            if (!await ApplyOfferAsync(ctx, pc, req.BenefitKind, req.BillingPlanKey, req.DiscountPercent, req.DiscountMonths, req.AppleOfferId, req.GoogleOfferId).ConfigureAwait(false)) return;
            pc = await Server.Database.PromoCodes.UpdateAsync(pc, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeChanged, 200, "Promotional code " + pc.Code + " updated.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await Server.PromoCodes.StatsAsync(pc, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            PromoCode? pc = await CodeAsync(ctx).ConfigureAwait(false);
            if (pc == null) return;
            bool force = QueryBool(ctx, "force") == true;
            if (pc.RedemptionCount > 0 && !force) { await SendErrorAsync(ctx, 409, "Conflict", "This code has been used; disable it instead so its history stays, or delete with force=true to revoke and remove its redemptions.").ConfigureAwait(false); return; }
            while (true)
            {
                EnumerationResult<PromoRedemption> page = await Server.Database.PromoRedemptions.EnumerateAsync(new PromoRedemptionFilter { AllTenants = true, PromoCodeId = pc.Id }, ctx.Token).ConfigureAwait(false);
                if (page.Objects.Count == 0) break;
                foreach (PromoRedemption r in page.Objects)
                {
                    await Server.PromoCodes.RevokeAsync(r, ctx.Token).ConfigureAwait(false);
                    await Server.Database.PromoRedemptions.DeleteAsync(r.TenantId, r.Id, ctx.Token).ConfigureAwait(false);
                }
            }

            await Server.Database.PromoCodes.DeleteAsync(pc.Id, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeChanged, 204, "Promotional code " + pc.Code + " deleted.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task RedemptionsAsync(HttpContextBase ctx)
        {
            PromoCode? pc = await CodeAsync(ctx).ConfigureAwait(false);
            if (pc == null) return;
            PromoRedemptionFilter query = new PromoRedemptionFilter { AllTenants = true, PromoCodeId = pc.Id };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.PromoRedemptions.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RevokeAsync(HttpContextBase ctx)
        {
            PromoCode? pc = await CodeAsync(ctx).ConfigureAwait(false);
            if (pc == null) return;
            PromoRedemption? r = await Server.Database.PromoRedemptions.ReadByIdAsync(Param(ctx, "redemptionId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (r == null || r.PromoCodeId != pc.Id) { await SendNotFoundAsync(ctx, "Redemption").ConfigureAwait(false); return; }
            r = await Server.PromoCodes.RevokeAsync(r, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeRedeemed, 200, "Redemption " + r.Id + " of " + pc.Code + " revoked for user " + r.UserId + ".").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, r).ConfigureAwait(false);
        }

        private async Task ApplyAsync(HttpContextBase ctx)
        {
            PromoCode? pc = await CodeAsync(ctx).ConfigureAwait(false);
            if (pc == null) return;
            PromoApplyRequest? req = await ReadBodyAsync<PromoApplyRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            User? u = await Server.Database.Users.ReadAsync(req.TenantId ?? String.Empty, req.UserId ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (u == null) { await SendNotFoundAsync(ctx, "User").ConfigureAwait(false); return; }
            PromoRedemption r = await Server.PromoCodes.RedeemAsync(u.TenantId, u.Id, pc.Code, PromoChannelEnum.Admin, false, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeRedeemed, 201, "Promotional code " + pc.Code + " applied to user " + u.Id + " by an administrator.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, r).ConfigureAwait(false);
        }

        private async Task ListPlansAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PlanFilter query = new PlanFilter { SortBy = "sortOrder", SortDirection = SortDirectionEnum.Ascending };
            ApplyPaging(ctx, query);
            EnumerationResult<Plan> page = await Server.Database.Plans.EnumerateAsync(query, ctx.Token).ConfigureAwait(false);
            List<PlanResponse> items = new List<PlanResponse>();
            foreach (Plan plan in page.Objects) items.Add(await DescribePlanAsync(plan, ctx).ConfigureAwait(false));
            await SendJsonAsync(ctx, 200, EnumerationResult<PlanResponse>.Create(items, query, page.TotalRecords, page.TotalMs)).ConfigureAwait(false);
        }

        private async Task UserEntitlementsAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            string tenantId = Param(ctx, "tenantId") ?? String.Empty;
            string userId = Param(ctx, "userId") ?? String.Empty;
            if (await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false) == null) { await SendNotFoundAsync(ctx, "User").ConfigureAwait(false); return; }
            await SendJsonAsync(ctx, 200, await Server.Entitlements.GetEffectiveAsync(tenantId, userId, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task SetOverrideAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            string tenantId = Param(ctx, "tenantId") ?? String.Empty;
            string userId = Param(ctx, "userId") ?? String.Empty;
            if (await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false) == null) { await SendNotFoundAsync(ctx, "User").ConfigureAwait(false); return; }
            EntitlementOverrideRequest? req = await ReadBodyAsync<EntitlementOverrideRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Reason) || req.ExpiresUtc <= DateTime.UtcNow)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "A reason and, if supplied, a future expiry are required.").ConfigureAwait(false);
                return;
            }

            string key = (req.FeatureKey ?? String.Empty).Trim();
            if (key == "topics.max") key = "customTopics.max";
            string value = PlanFeatureCatalog.Normalize(key, req.Value);
            EntitlementOverride? item = (await Server.Database.EntitlementOverrides.EnumerateAsync(new EntitlementOverrideFilter { TenantId = tenantId, UserId = userId, FeatureKey = key, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
            bool create = item == null;
            item ??= new EntitlementOverride { TenantId = tenantId, UserId = userId, FeatureKey = key };
            item.Value = value;
            item.Reason = req.Reason.Trim();
            item.ExpiresUtc = req.ExpiresUtc;
            item.GrantedByUserId = Context(ctx).UserId;
            item.Active = true;
            if (create) await Server.Database.EntitlementOverrides.CreateAsync(item, ctx.Token).ConfigureAwait(false);
            else await Server.Database.EntitlementOverrides.UpdateAsync(item, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PlanChanged, 200, "Override " + key + "=" + value + " for " + userId + ": " + item.Reason).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await Server.Entitlements.GetEffectiveAsync(tenantId, userId, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task DeleteOverrideAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            string tenantId = Param(ctx, "tenantId") ?? String.Empty;
            string userId = Param(ctx, "userId") ?? String.Empty;
            string key = Param(ctx, "featureKey") ?? String.Empty;
            if (key == "topics.max") key = "customTopics.max";
            if (await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false) == null) { await SendNotFoundAsync(ctx, "User").ConfigureAwait(false); return; }
            EntitlementOverride? item = (await Server.Database.EntitlementOverrides.EnumerateAsync(new EntitlementOverrideFilter { TenantId = tenantId, UserId = userId, FeatureKey = key, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
            if (item != null) await Server.Database.EntitlementOverrides.DeleteAsync(tenantId, item.Id, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PlanChanged, 204, "Restored plan inheritance for " + key + " for " + userId + ".").ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task CreatePlanAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PlanUpsertRequest? req = await ReadBodyAsync<PlanUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            ValidateFeatures(req.Features);
            string key = (req.Key ?? String.Empty).Trim().ToLowerInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9][a-z0-9-]{1,62}$") || String.IsNullOrWhiteSpace(req.Name))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "key (lowercase letters, digits, dashes) and name are required.").ConfigureAwait(false);
                return;
            }

            if ((await Server.Database.Plans.EnumerateAsync(new PlanFilter { Key = key, PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.Count > 0) { await SendErrorAsync(ctx, 409, "Conflict", "A plan with that key exists.").ConfigureAwait(false); return; }
            Plan p = await Server.Database.Plans.CreateAsync(new Plan { Key = key, Name = req.Name.Trim(), Description = req.Description, SortOrder = 10 }, ctx.Token).ConfigureAwait(false);
            await SetFeaturesAsync(p, req.Features, ctx).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PlanChanged, 201, "Plan " + key + " created.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await DescribePlanAsync(p, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpdatePlanAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            Plan? p = await Server.Database.Plans.ReadAsync(Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (p == null) { await SendNotFoundAsync(ctx, "Plan").ConfigureAwait(false); return; }
            PlanUpsertRequest? req = await ReadBodyAsync<PlanUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            ValidateFeatures(req.Features);
            if (req.Key != null && req.Key.Trim().ToLowerInvariant() != p.Key) { await SendErrorAsync(ctx, 400, "ValidationFailed", "A plan key cannot change.").ConfigureAwait(false); return; }
            if (!String.IsNullOrWhiteSpace(req.Name)) p.Name = req.Name.Trim();
            if (req.Description != null) p.Description = req.Description.Trim().Length == 0 ? null : req.Description.Trim();
            p = await Server.Database.Plans.UpdateAsync(p, ctx.Token).ConfigureAwait(false);
            await SetFeaturesAsync(p, req.Features, ctx).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PlanChanged, 200, "Plan " + p.Key + " updated.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, await DescribePlanAsync(p, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListAssignmentsAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            string? tenantId = Query(ctx, "tenantId");
            string? userId = Query(ctx, "userId");
            if (tenantId == null || userId == null) { await SendErrorAsync(ctx, 400, "ValidationFailed", "tenantId and userId are required.").ConfigureAwait(false); return; }
            PlanAssignmentFilter query = new PlanAssignmentFilter { TenantId = tenantId, UserId = userId, SortBy = "startsUtc" };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.PlanAssignments.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task GrantAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PlanGrantRequest? req = await ReadBodyAsync<PlanGrantRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            User? u = await Server.Database.Users.ReadAsync(req.TenantId ?? String.Empty, req.UserId ?? String.Empty, ctx.Token).ConfigureAwait(false);
            Plan? p = await Server.Database.Plans.ReadAsync(req.PlanId ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (u == null || p == null) { await SendErrorAsync(ctx, 400, "ValidationFailed", "The user or plan was not found.").ConfigureAwait(false); return; }
            if (req.DurationDays.HasValue && (req.DurationDays < 0 || req.DurationDays > 3650)) { await SendErrorAsync(ctx, 400, "ValidationFailed", "durationDays must be 0 to 3650.").ConfigureAwait(false); return; }
            DateTime now = DateTime.UtcNow;
            if (req.ReplaceCurrent)
            {
                foreach (PlanAssignment old in (await Server.Entitlements.AssignmentsAsync(u.TenantId, u.Id, ctx.Token).ConfigureAwait(false))
                    .Where(x => x.Source == PlanSourceEnum.Admin && !x.RevokedUtc.HasValue && (!x.EndsUtc.HasValue || x.EndsUtc > now)))
                {
                    old.RevokedUtc = now;
                    await Server.Database.PlanAssignments.UpdateAsync(old, ctx.Token).ConfigureAwait(false);
                }
            }

            PlanAssignment a = await Server.Database.PlanAssignments.CreateAsync(new PlanAssignment
            {
                TenantId = u.TenantId, UserId = u.Id, PlanId = p.Id, Source = PlanSourceEnum.Admin, StartsUtc = now,
                EndsUtc = req.DurationDays.HasValue && req.DurationDays > 0 ? now.AddDays(req.DurationDays.Value) : null, Note = req.Note, GrantedByUserId = Context(ctx).UserId
            }, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PlanChanged, 201, "Plan " + p.Key + " given to user " + u.Id + ".").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, a).ConfigureAwait(false);
        }

        private async Task RevokeAssignmentAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            PlanAssignment? a = await Server.Database.PlanAssignments.ReadAsync(Query(ctx, "tenantId") ?? String.Empty, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (a == null) { await SendNotFoundAsync(ctx, "Plan assignment").ConfigureAwait(false); return; }
            if (!a.RevokedUtc.HasValue)
            {
                a.RevokedUtc = DateTime.UtcNow;
                await Server.Database.PlanAssignments.UpdateAsync(a, ctx.Token).ConfigureAwait(false);
                await AuditAsync(ctx, AuditEventTypeEnum.PlanChanged, 204, "Plan assignment " + a.Id + " revoked.").ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task RedeemAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            PromoRedeemRequest? req = await ReadBodyAsync<PromoRedeemRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string userId = Context(ctx).UserId ?? String.Empty;
            if (!_Limiter.TryAcquire("redeem:" + userId)) { await SendErrorAsync(ctx, 429, "TooManyRequests", "Too many attempts; try again in a minute.").ConfigureAwait(false); return; }
            PromoRedemption r = await Server.PromoCodes.RedeemAsync(tenantId, userId, req.Code, PromoChannelEnum.Account, false, ctx.Token).ConfigureAwait(false);
            await AuditAsync(ctx, AuditEventTypeEnum.PromoCodeRedeemed, 201, "Promotional code " + r.Code + " redeemed.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, await Server.Entitlements.GetEffectiveAsync(tenantId, userId, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task MyPlanAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            await SendJsonAsync(ctx, 200, await Server.Entitlements.GetEffectiveAsync(tenantId, Context(ctx).UserId ?? String.Empty, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CheckAsync(HttpContextBase ctx)
        {
            if (!_Limiter.TryAcquire("check:" + (ctx.Request.Source?.IpAddress ?? "unknown")))
            {
                await SendErrorAsync(ctx, 429, "TooManyRequests", "Too many checks; try again in a minute.").ConfigureAwait(false);
                return;
            }

            PromoCheck check = await Server.PromoCodes.CheckAsync(Param(ctx, "code"), null, null, true, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, check).ConfigureAwait(false);
        }

        private async Task<bool> ApplyOfferAsync(HttpContextBase ctx, PromoCode code, string? kind, string? billingPlan, decimal? percent, int? months, string? appleOffer, string? googleOffer)
        {
            if (kind != null) code.BenefitKind = kind;
            if (billingPlan != null) code.BillingPlanKey = String.IsNullOrWhiteSpace(billingPlan) ? null : billingPlan.Trim();
            if (percent.HasValue) code.DiscountPercent = percent.Value;
            if (months.HasValue) code.DiscountMonths = months == 0 ? null : months;
            if (appleOffer != null) code.AppleOfferId = String.IsNullOrWhiteSpace(appleOffer) ? null : appleOffer.Trim();
            if (googleOffer != null) code.GoogleOfferId = String.IsNullOrWhiteSpace(googleOffer) ? null : googleOffer.Trim();
            string? error = null;
            if (code.BenefitKind != "Complimentary" && code.BenefitKind != "PaidOffer") error = "benefitKind must be Complimentary or PaidOffer.";
            else if (code.DiscountPercent < 0 || code.DiscountPercent > 100 || Decimal.Round(code.DiscountPercent, 2) != code.DiscountPercent) error = "discountPercent must be 0 to 100, with at most two decimal places.";
            else if (code.DiscountMonths.HasValue && (code.DiscountMonths < 1 || code.DiscountMonths > 36)) error = "discountMonths must be 0 (ongoing) or 1 to 36.";
            else if ((code.AppleOfferId?.Length ?? 0) > 128 || (code.GoogleOfferId?.Length ?? 0) > 128) error = "Native offer identifiers must be at most 128 characters.";
            else if (code.BenefitKind == "PaidOffer")
            {
                if (String.IsNullOrWhiteSpace(code.BillingPlanKey) || !Server.Settings.Billing.Offers.Any(o => o.Active && o.PlanKey == code.BillingPlanKey))
                    error = "Select a configured paid package for billingPlanKey.";
                code.NewAccountsOnly = true;
                code.PerUserLimit = 1;
            }
            else if (code.DiscountPercent != 0 || code.DiscountMonths.HasValue || code.BillingPlanKey != null || code.AppleOfferId != null || code.GoogleOfferId != null)
                error = "Discounts and payment mappings require benefitKind PaidOffer.";
            if (error == null) return true;
            await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false);
            return false;
        }

        private async Task<bool> ApplyAsync(HttpContextBase ctx, PromoCode pc, string? description, string? planId, int? durationDays, int? maxRedemptions, int? perUserLimit, DateTime? startsUtc, DateTime? expiresUtc, bool? enabled, bool? newOnly, bool create)
        {
            if (description != null) pc.Description = description.Trim().Length == 0 ? null : description.Trim();
            if (planId != null || create)
            {
                Plan? plan = planId != null ? await Server.Database.Plans.ReadAsync(planId, ctx.Token).ConfigureAwait(false)
                    : (await Server.Database.Plans.EnumerateAsync(new PlanFilter { Key = "complimentary", PageSize = 1 }, ctx.Token).ConfigureAwait(false)).Objects.FirstOrDefault();
                if (plan == null) { await SendErrorAsync(ctx, 400, "ValidationFailed", "The plan was not found.").ConfigureAwait(false); return false; }
                pc.PlanId = plan.Id;
            }

            if (durationDays.HasValue)
            {
                if (durationDays < 0 || durationDays > 3650) { await SendErrorAsync(ctx, 400, "ValidationFailed", "durationDays must be 0 (no end) to 3650.").ConfigureAwait(false); return false; }
                pc.DurationDays = durationDays == 0 ? null : durationDays;
            }
            else if (create && !pc.DurationDays.HasValue)
            {
                pc.DurationDays = 30;
            }

            if (maxRedemptions.HasValue)
            {
                if (maxRedemptions < 0 || maxRedemptions > 10000000) { await SendErrorAsync(ctx, 400, "ValidationFailed", "maxRedemptions must be 0 (unlimited) or more.").ConfigureAwait(false); return false; }
                if (maxRedemptions > 0 && maxRedemptions < pc.RedemptionCount) { await SendErrorAsync(ctx, 400, "ValidationFailed", "maxRedemptions cannot be below the " + pc.RedemptionCount + " redemptions already made.").ConfigureAwait(false); return false; }
                pc.MaxRedemptions = maxRedemptions == 0 ? null : maxRedemptions;
            }

            if (perUserLimit.HasValue) pc.PerUserLimit = perUserLimit.Value;
            if (startsUtc.HasValue) pc.StartsUtc = DateTime.SpecifyKind(startsUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (expiresUtc.HasValue) pc.ExpiresUtc = DateTime.SpecifyKind(expiresUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (pc.StartsUtc.HasValue && pc.ExpiresUtc.HasValue && pc.ExpiresUtc <= pc.StartsUtc) { await SendErrorAsync(ctx, 400, "ValidationFailed", "expiresUtc must be after startsUtc.").ConfigureAwait(false); return false; }
            if (enabled.HasValue) pc.Enabled = enabled.Value;
            if (newOnly.HasValue) pc.NewAccountsOnly = newOnly.Value;
            return true;
        }

        private async Task SetFeaturesAsync(Plan p, Dictionary<string, string>? features, HttpContextBase ctx)
        {
            if (features == null) return;
            List<PlanFeature> have = (await Server.Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = p.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Objects;
            foreach (KeyValuePair<string, string> kv in features)
            {
                string key = (kv.Key ?? String.Empty).Trim();
                if (key.Length == 0 || key.Length > 128) continue;
                PlanFeature? f = have.FirstOrDefault(h => h.Key == key);
                if (String.IsNullOrWhiteSpace(kv.Value))
                {
                    if (f != null) await Server.Database.PlanFeatures.DeleteAsync(f.Id, ctx.Token).ConfigureAwait(false);
                }
                else if (f == null)
                {
                    await Server.Database.PlanFeatures.CreateAsync(new PlanFeature { PlanId = p.Id, Key = key, Value = PlanFeatureCatalog.Normalize(key, kv.Value) }, ctx.Token).ConfigureAwait(false);
                }
                else
                {
                    f.Value = PlanFeatureCatalog.Normalize(key, kv.Value);
                    await Server.Database.PlanFeatures.UpdateAsync(f, ctx.Token).ConfigureAwait(false);
                }
            }
        }

        private static void ValidateFeatures(Dictionary<string, string>? features)
        {
            if (features == null) return;
            foreach (KeyValuePair<string, string> item in features)
                if (!String.IsNullOrWhiteSpace(item.Value)) PlanFeatureCatalog.Normalize(item.Key, item.Value);
        }

        private async Task<PlanResponse> DescribePlanAsync(Plan p, HttpContextBase ctx)
        {
            Dictionary<string, string> features = (await Server.Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = p.Id, PageSize = 500 }, ctx.Token).ConfigureAwait(false)).Objects
                .ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
            return new PlanResponse { Plan = p, Features = features };
        }

        private static string StatusOf(PromoCode c, DateTime now)
        {
            if (!c.Enabled) return "disabled";
            if (c.StartsUtc.HasValue && c.StartsUtc > now) return "notstarted";
            if (c.ExpiresUtc.HasValue && c.ExpiresUtc <= now) return "expired";
            if (c.MaxRedemptions.HasValue && c.RedemptionCount >= c.MaxRedemptions.Value) return "exhausted";
            return "active";
        }

        private async Task<PromoCode?> CodeAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return null;
            PromoCode? pc = await Server.Database.PromoCodes.ReadAsync(Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (pc == null) await SendNotFoundAsync(ctx, "Promotional code").ConfigureAwait(false);
            return pc;
        }

        private async Task<bool> AdminAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeAdmin).ConfigureAwait(false)) return false;
            if (Context(ctx).IsAdmin) return true;
            await SendErrorAsync(ctx, 403, "Forbidden", "Only global administrators manage plans and promotional codes.").ConfigureAwait(false);
            return false;
        }

        private async Task AuditAsync(HttpContextBase ctx, AuditEventTypeEnum type, int status, string reason)
        {
            await Server.Audit.RecordAsync(type, Context(ctx), status, reason, ctx.Token).ConfigureAwait(false);
        }
    }
}
