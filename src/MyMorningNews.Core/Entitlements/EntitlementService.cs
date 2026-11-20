namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
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
                { "usage.monthlyCostUsd.max", "3.00" }, { "topics.max", "1" }, { "schedule.minIntervalMinutes", "1440" }, { "runs.daily.max", "2" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "10" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "false" }, { "artifacts.pinned.max", "50" }
            }),
            new PlanSeed("complimentary", "Complimentary", "A free subscription given by promotional codes, a step up from Free.", 2, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "10.00" }, { "topics.max", "2" }, { "schedule.minIntervalMinutes", "1440" }, { "runs.daily.max", "4" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "30" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "false" }, { "artifacts.pinned.max", "200" }
            }),
            new PlanSeed("unlimited", "Unlimited", "No caps. The default for self-hosted installs.", 3, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "unlimited" }, { "topics.max", "unlimited" }, { "schedule.minIntervalMinutes", "unlimited" }, { "runs.daily.max", "unlimited" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "unlimited" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "true" }, { "artifacts.pinned.max", "10000" }
            })
        };

        /// <summary>
        /// Values earlier releases shipped for built-in plans, by plan key and feature key. A stored value that still
        /// equals one of these was never edited, so startup moves it to the current default (owner decision 2026-11-20).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReplacedDefaults = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            { "free", new Dictionary<string, string>(StringComparer.Ordinal) { { "usage.monthlyCostUsd.max", "5.00" }, { "topics.max", "5" }, { "schedule.minIntervalMinutes", "360" }, { "runs.daily.max", "8" }, { "audio.monthlyMinutes.max", "120" } } },
            { "complimentary", new Dictionary<string, string>(StringComparer.Ordinal) { { "usage.monthlyCostUsd.max", "30.00" }, { "topics.max", "25" }, { "schedule.minIntervalMinutes", "60" }, { "runs.daily.max", "48" }, { "audio.monthlyMinutes.max", "600" } } },
            { "unlimited", new Dictionary<string, string>(StringComparer.Ordinal) { { "schedule.minIntervalMinutes", "5" } } }
        };

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private readonly ConcurrentDictionary<string, CachedCost> _CostCache = new ConcurrentDictionary<string, CachedCost>(StringComparer.Ordinal);

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
        /// Existing values are kept (administrators may have edited them), except values still equal to a default an
        /// earlier release shipped (<see cref="ReplacedDefaults"/>), which move to the current default.
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
                if (ReplacedDefaults.TryGetValue(seed.Key, out IReadOnlyDictionary<string, string>? old))
                {
                    foreach (PlanFeature f in have.Where(h => old.TryGetValue(h.Key, out string? was) && was == h.Value && seed.Features.ContainsKey(h.Key) && seed.Features[h.Key] != h.Value))
                    {
                        f.Value = seed.Features[f.Key];
                        await _Database.PlanFeatures.UpdateAsync(f, token).ConfigureAwait(false);
                    }
                }
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
        /// A numeric plan limit, or null when the plan does not limit it ("unlimited", empty, or missing).
        /// </summary>
        /// <param name="plan">Effective plan.</param>
        /// <param name="key">Entitlement key.</param>
        /// <returns>Limit or null.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static decimal? Limit(EffectivePlan plan, string key)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(key);
            if (!plan.Features.TryGetValue(key, out string? raw) || String.IsNullOrWhiteSpace(raw)) return null;
            if (String.Equals(raw.Trim(), "unlimited", StringComparison.OrdinalIgnoreCase)) return null;
            return Decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v) ? v : null;
        }

        /// <summary>
        /// A boolean plan feature; missing means allowed.
        /// </summary>
        /// <param name="plan">Effective plan.</param>
        /// <param name="key">Entitlement key.</param>
        /// <returns>True when allowed.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static bool Allowed(EffectivePlan plan, string key)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(key);
            return !plan.Features.TryGetValue(key, out string? raw) || !String.Equals(raw?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The first day of next month (UTC): when monthly limits reset.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <returns>Reset time.</returns>
        public static DateTime MonthlyReset(DateTime nowUtc)
        {
            DateTime start = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return start.AddMonths(1);
        }

        /// <summary>
        /// Model cost attributed to the user this calendar month (UTC), cached for 30 seconds.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Cost in USD.</returns>
        public async Task<decimal> MonthToDateCostAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string key = tenantId + "|" + userId;
            DateTime now = DateTime.UtcNow;
            if (_CostCache.TryGetValue(key, out CachedCost? c) && c.ExpiresUtc > now) return c.CostUsd;
            DateTime start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            UsageReport report = await _Database.UsageAnalytics.ReportAsync(new UsageReportFilter { TenantId = tenantId, UserId = userId, FromUtc = start, ToUtc = now.AddMinutes(1), Top = 1 }, token).ConfigureAwait(false);
            _CostCache[key] = new CachedCost(report.Totals.CostUsd, now.AddSeconds(30));
            return report.Totals.CostUsd;
        }

        /// <summary>
        /// Refuse when the user's month-to-date model cost has reached the plan's cap.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="CapReachedException">The cap is reached.</exception>
        public async Task CheckCostAsync(string tenantId, string userId, CancellationToken token = default)
        {
            EffectivePlan plan = await GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            decimal? cap = Limit(plan, "usage.monthlyCostUsd.max");
            if (cap == null) return;
            decimal spent = await MonthToDateCostAsync(tenantId, userId, token).ConfigureAwait(false);
            DateTime reset = MonthlyReset(DateTime.UtcNow);
            if (spent >= cap.Value)
                throw new CapReachedException("usage.monthlyCostUsd.max", cap.Value.ToString("0.00", CultureInfo.InvariantCulture), reset,
                    "You have used this month's research budget ($" + cap.Value.ToString("0.00", CultureInfo.InvariantCulture) + ") on the " + plan.Plan.Name + " plan. It resets on " + reset.ToString("MMMM d", CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>
        /// Refuse when adding one more of something would pass a plan count limit.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="key">Entitlement key (topics.max or runs.daily.max).</param>
        /// <param name="current">Current count.</param>
        /// <param name="what">What is limited, for the message (for example "topics").</param>
        /// <param name="resetUtc">When the count resets, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="CapReachedException">The limit is reached.</exception>
        public async Task CheckCountAsync(string tenantId, string userId, string key, long current, string what, DateTime? resetUtc, CancellationToken token = default)
        {
            EffectivePlan plan = await GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            decimal? max = Limit(plan, key);
            if (max == null || current < max.Value) return;
            throw new CapReachedException(key, max.Value.ToString("0", CultureInfo.InvariantCulture), resetUtc,
                "The " + plan.Plan.Name + " plan allows " + max.Value.ToString("0", CultureInfo.InvariantCulture) + " " + what + "." + (resetUtc.HasValue ? " It resets on " + resetUtc.Value.ToString("MMMM d, HH:mm 'UTC'", CultureInfo.InvariantCulture) + "." : String.Empty));
        }

        /// <summary>
        /// The usage meter for a user.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Meter.</returns>
        public async Task<UsageMeter> MeterAsync(string tenantId, string userId, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            DateTime monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            EffectivePlan plan = await GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            _CostCache.TryRemove(tenantId + "|" + userId, out CachedCost? _);
            decimal spent = await MonthToDateCostAsync(tenantId, userId, token).ConfigureAwait(false);
            decimal? cap = Limit(plan, "usage.monthlyCostUsd.max");
            List<MediaAsset> audio = (await _Database.MediaAssets.EnumerateAsync(new MediaAssetFilter { TenantId = tenantId, UserId = userId, FromUtc = monthStart, PageSize = 5000 }, token).ConfigureAwait(false)).Items;
            return new UsageMeter
            {
                PlanKey = plan.Plan.Key,
                PlanName = plan.Plan.Name,
                MonthToDateCostUsd = spent,
                MonthlyCostCapUsd = cap,
                CostUsedFraction = cap.HasValue && cap.Value > 0 ? Math.Round((double)(spent / cap.Value), 4) : null,
                ResetUtc = MonthlyReset(now),
                Topics = await _Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId, IsOneTime = false }, token).ConfigureAwait(false),
                TopicsMax = (long?)Limit(plan, "topics.max"),
                RunsToday = await _Database.Runs.CountAsync(new RunFilter { TenantId = tenantId, UserId = userId, IsDryRun = false, FromUtc = now.Date }, token).ConfigureAwait(false),
                RunsDailyMax = (long?)Limit(plan, "runs.daily.max"),
                ScheduleMinIntervalMinutes = (long?)Limit(plan, "schedule.minIntervalMinutes"),
                AudioMinutes = Math.Round(audio.Sum(a => a.DurationSeconds) / 60.0, 1),
                AudioMinutesMax = (long?)Limit(plan, "audio.monthlyMinutes.max"),
                AudioEnabled = Allowed(plan, "audio.enabled"),
                CustomEndpoints = Allowed(plan, "endpoints.custom")
            };
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
            EffectivePlan result = new EffectivePlan { Plan = plan, Features = features, Assignment = current, EndsUtc = current?.EndsUtc, Upcoming = live.Where(a => a.StartsUtc > now).OrderBy(a => a.StartsUtc).ToList() };
            result.PlanNames[plan.Id] = plan.Name;
            foreach (string id in result.Upcoming.Select(a => a.PlanId).Distinct().Where(id => !result.PlanNames.ContainsKey(id)))
            {
                Plan? p = await _Database.Plans.ReadAsync(id, token).ConfigureAwait(false);
                if (p != null) result.PlanNames[id] = p.Name;
            }

            return result;
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
