namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Plans and plan assignments (plan section 33.2): seeds the built-in plans and resolves the plan a user has now.
    /// An active assignment (administrator, promotional code, or billing later) wins over the default plan; the newest
    /// started one wins among several. Enforcement of the caps arrives with the hosted service (Phase 14).
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class EntitlementService
    {
        /// <summary>
        /// Built-in plans: key, name, description, and entitlement values.
        /// </summary>
        public static readonly IReadOnlyList<PlanSeed> BuiltIn = new List<PlanSeed>
        {
            new PlanSeed("free", "Free", "The hosted service's free plan with monthly caps.", 1, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "5.00" }, { "topics.max", "5" }, { "schedule.minIntervalMinutes", "360" }, { "runs.daily.max", "8" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "120" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "false" }, { "artifacts.pinned.max", "50" }
            }),
            new PlanSeed("complimentary", "Complimentary", "A free subscription with generous limits, given by promotional codes.", 2, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "30.00" }, { "topics.max", "25" }, { "schedule.minIntervalMinutes", "60" }, { "runs.daily.max", "48" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "600" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "false" }, { "artifacts.pinned.max", "200" }
            }),
            new PlanSeed("unlimited", "Unlimited", "No caps. The default for self-hosted installs.", 3, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "unlimited" }, { "topics.max", "unlimited" }, { "schedule.minIntervalMinutes", "5" }, { "runs.daily.max", "unlimited" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "unlimited" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "true" }, { "artifacts.pinned.max", "10000" }
            })
        };

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Server settings.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public EntitlementService(DatabaseDriverBase database, ServerSettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Create missing built-in plans and their missing feature values, and mark the configured default.
        /// Existing values are never changed (administrators may have edited them).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Plans created.</returns>
        public async Task<int> EnsureDefaultsAsync(CancellationToken token = default)
        {
            int created = 0;
            List<Plan> plans = (await _Database.Plans.EnumerateAsync(new PlanFilter { PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            foreach (PlanSeed seed in BuiltIn)
            {
                Plan? plan = plans.FirstOrDefault(p => p.Key == seed.Key);
                if (plan == null)
                {
                    plan = await _Database.Plans.CreateAsync(new Plan { Key = seed.Key, Name = seed.Name, Description = seed.Description, SortOrder = seed.SortOrder }, token).ConfigureAwait(false);
                    plans.Add(plan);
                    created++;
                }

                List<PlanFeature> have = (await _Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = plan.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items;
                foreach (KeyValuePair<string, string> kv in seed.Features.Where(kv => have.All(h => h.Key != kv.Key)))
                    await _Database.PlanFeatures.CreateAsync(new PlanFeature { PlanId = plan.Id, Key = kv.Key, Value = kv.Value }, token).ConfigureAwait(false);
            }

            foreach (Plan p in plans)
            {
                bool isDefault = p.Key == _Settings.Hosting.DefaultPlanKey;
                if (p.IsDefault == isDefault) continue;
                p.IsDefault = isDefault;
                await _Database.Plans.UpdateAsync(p, token).ConfigureAwait(false);
            }

            return created;
        }

        /// <summary>
        /// The plan a user has now.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Effective plan.</returns>
        /// <exception cref="InvalidOperationException">No plans exist.</exception>
        public async Task<EffectivePlan> GetEffectiveAsync(string tenantId, string userId, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            List<PlanAssignment> all = await AssignmentsAsync(tenantId, userId, token).ConfigureAwait(false);
            List<PlanAssignment> live = all.Where(a => !a.RevokedUtc.HasValue && (!a.EndsUtc.HasValue || a.EndsUtc > now)).ToList();
            PlanAssignment? current = live.Where(a => a.StartsUtc <= now).OrderByDescending(a => a.StartsUtc).ThenByDescending(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            Plan? plan = current == null ? null : await _Database.Plans.ReadAsync(current.PlanId, token).ConfigureAwait(false);
            if (plan == null)
            {
                current = null;
                plan = await DefaultPlanAsync(token).ConfigureAwait(false);
            }

            Dictionary<string, string> features = (await _Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = plan.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items
                .ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
            return new EffectivePlan { Plan = plan, Features = features, Assignment = current, EndsUtc = current?.EndsUtc, Upcoming = live.Where(a => a.StartsUtc > now).OrderBy(a => a.StartsUtc).ToList() };
        }

        /// <summary>
        /// The default plan (Hosting.DefaultPlanKey), seeding built-ins when none exist.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Plan.</returns>
        public async Task<Plan> DefaultPlanAsync(CancellationToken token = default)
        {
            List<Plan> plans = (await _Database.Plans.EnumerateAsync(new PlanFilter { PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            if (plans.Count == 0)
            {
                await EnsureDefaultsAsync(token).ConfigureAwait(false);
                plans = (await _Database.Plans.EnumerateAsync(new PlanFilter { PageSize = 1000 }, token).ConfigureAwait(false)).Items;
            }

            return plans.FirstOrDefault(p => p.Key == _Settings.Hosting.DefaultPlanKey) ?? plans.FirstOrDefault(p => p.IsDefault) ?? plans.OrderBy(p => p.SortOrder).First();
        }

        /// <summary>
        /// A user's assignments, newest first.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Assignments.</returns>
        public async Task<List<PlanAssignment>> AssignmentsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return (await _Database.PlanAssignments.EnumerateAsync(new PlanAssignmentFilter { TenantId = tenantId, UserId = userId, PageSize = 500 }, token).ConfigureAwait(false)).Items
                .OrderByDescending(a => a.StartsUtc).ToList();
        }
    }
}
