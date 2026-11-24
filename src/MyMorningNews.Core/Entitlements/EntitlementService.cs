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
            Tier("free", "Free", "Five selected system briefing subscriptions with shared audio.", 1, "0", "5", "0", true, false),
            new PlanSeed("complimentary", "Complimentary", "A free subscription given by promotional codes, a step up from Free.", 2, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "10.00" }, { "topics.max", "2" }, { "schedule.minIntervalMinutes", "1440" }, { "runs.daily.max", "4" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "30" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "false" }, { "artifacts.pinned.max", "200" }
            }),
            new PlanSeed("unlimited", "Unlimited", "No caps. The default for self-hosted installs.", 3, new Dictionary<string, string>
            {
                { "usage.monthlyCostUsd.max", "unlimited" }, { "topics.max", "unlimited" }, { "schedule.minIntervalMinutes", "unlimited" }, { "runs.daily.max", "unlimited" },
                { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "unlimited" }, { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", "true" }, { "artifacts.pinned.max", "10000" }
            }),
            Tier("catalog-only", "Catalog only", "Five system briefing subscriptions without billing or custom research.", 4, "0", "5", "0", true, false),
            Tier("complimentary-five", "Complimentary Five", "Five system subscriptions and five custom briefings with unlimited audio, without billing.", 5, "5", "5", "unlimited", false, false),
            Tier("paid-5", "Paid 5", "Five custom briefings, system subscriptions, and 30 generated audio minutes per day.", 6, "5", "unlimited", "1800", false, false),
            Tier("paid-20", "Paid 20", "Twenty custom briefings, system subscriptions, and 100 generated audio minutes per day.", 7, "20", "unlimited", "6000", false, false),
            Tier("byok", "Bring your own key", "Unlimited custom briefings using your own model and speech resources.", 8, "unlimited", "unlimited", "unlimited", false, true),
            Tier("self-hosted", "Self-hosted", "Unlimited briefings using your deployment's resources.", 9, "unlimited", "unlimited", "unlimited", false, true)
        };

        private static PlanSeed Tier(string key, string name, string description, int order, string topics, string subscriptions, string audio, bool freeOnly, bool personal)
        {
            return new PlanSeed(key, name, description, order, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "topics.max", topics }, { "customTopics.max", topics }, { "systemSubscriptions.max", subscriptions },
                { "customResearch.enabled", topics == "0" ? "false" : "true" }, { "oneTimeResearch.enabled", topics == "0" ? "false" : "true" },
                { "audio.enabled", audio == "0" ? "false" : "true" }, { "audio.dailySeconds.max", audio },
                { "audio.monthlyMinutes.max", "unlimited" }, { "catalog.audio.enabled", "true" }, { "catalog.freeOnly", freeOnly ? "true" : "false" },
                { "usage.monthlyCostUsd.max", "unlimited" }, { "schedule.minIntervalMinutes", "unlimited" }, { "runs.daily.max", "unlimited" },
                { "email.enabled", "true" }, { "mobile.access", "true" }, { "endpoints.custom", personal ? "true" : "false" },
                { "models.personalConnections.enabled", "true" }, { "artifacts.pinned.max", "10000" }
            });
        }

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

        private static readonly Dictionary<string, string> LegacyFreeDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "usage.monthlyCostUsd.max", "3.00" }, { "topics.max", "1" }, { "schedule.minIntervalMinutes", "1440" },
            { "runs.daily.max", "2" }, { "audio.enabled", "true" }, { "audio.monthlyMinutes.max", "10" }, { "artifacts.pinned.max", "50" }
        };

        private async Task PreserveLegacyFreeAsync(List<Plan> plans, CancellationToken token)
        {
            Plan? free = plans.FirstOrDefault(p => p.Key == "free");
            if (free == null) return;
            List<PlanFeature> features = (await _Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = free.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items;
            if (features.Any(f => f.Key == "customResearch.enabled")) return;
            Plan? legacy = plans.FirstOrDefault(p => p.Key == "legacy-free");
            if (legacy == null)
            {
                legacy = await _Database.Plans.CreateAsync(new Plan { Key = "legacy-free", Name = "Legacy Free", Description = "Existing account allowances preserved during the catalog-tier migration.", SortOrder = 100 }, token).ConfigureAwait(false);
                plans.Add(legacy);
            }
            Dictionary<string, string> values = new Dictionary<string, string>(Tier("legacy-free", "Legacy Free", "", 100, "1", "5", "unlimited", true, false).Features, StringComparer.Ordinal);
            foreach (PlanFeature feature in features) values[feature.Key] = feature.Value;
            values["customTopics.max"] = values["topics.max"];
            List<PlanFeature> existing = (await _Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = legacy.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items;
            foreach (KeyValuePair<string, string> value in values.Where(v => !existing.Any(f => f.Key == v.Key)))
                await _Database.PlanFeatures.CreateAsync(new PlanFeature { PlanId = legacy.Id, Key = value.Key, Value = value.Value }, token).ConfigureAwait(false);
            for (int page = 1; ; page++)
            {
                EnumerationResult<Tenant> tenants = await _Database.Tenants.EnumerateAsync(new TenantFilter { PageNumber = page, PageSize = 1000 }, token).ConfigureAwait(false);
                foreach (Tenant tenant in tenants.Items)
                {
                    for (int userPage = 1; ; userPage++)
                    {
                        EnumerationResult<User> users = await _Database.Users.EnumerateAsync(new UserFilter { TenantId = tenant.Id, PageNumber = userPage, PageSize = 1000 }, token).ConfigureAwait(false);
                        foreach (User user in users.Items)
                        {
                            if (_Settings.Hosting.DefaultPlanKey == "free" && await _Database.AccountPlanPolicies.CountAsync(new AccountPlanPolicyFilter { TenantId = tenant.Id, UserId = user.Id }, token).ConfigureAwait(false) == 0)
                                await _Database.AccountPlanPolicies.CreateAsync(new AccountPlanPolicy { TenantId = tenant.Id, UserId = user.Id, DefaultPlanId = legacy.Id, SubscriptionRequired = false, FundingMode = "Platform" }, token).ConfigureAwait(false);
                            List<PlanAssignment> assignments = (await _Database.PlanAssignments.EnumerateAsync(new PlanAssignmentFilter { TenantId = tenant.Id, UserId = user.Id, PlanId = free.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
                            foreach (PlanAssignment assignment in assignments)
                            {
                                assignment.PlanId = legacy.Id;
                                assignment.Note = (assignment.Note ?? String.Empty) + " Preserved by catalog-tier migration.";
                                await _Database.PlanAssignments.UpdateAsync(assignment, token).ConfigureAwait(false);
                            }
                        }
                        if (userPage >= users.TotalPages) break;
                    }
                }
                if (page >= tenants.TotalPages) break;
            }
        }

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
            await PreserveLegacyFreeAsync(plans, token).ConfigureAwait(false);
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
                    foreach (PlanFeature f in have.Where(h => (old.TryGetValue(h.Key, out string? was) && was == h.Value || seed.Key == "free" && LegacyFreeDefaults.TryGetValue(h.Key, out string? recent) && recent == h.Value) && seed.Features.ContainsKey(h.Key) && seed.Features[h.Key] != h.Value))
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

        /// <summary>Refuse custom or one-time research before queueing or making a model call. Catalog reads do not use this gate.</summary>
        public async Task CheckResearchAsync(string tenantId, string userId, bool oneTime, CancellationToken token = default)
        {
            EffectivePlan plan = await GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            string feature = oneTime ? "oneTimeResearch.enabled" : "customResearch.enabled";
            if (!Allowed(plan, feature)) throw new CapReachedException(feature, "false", null, "Your plan includes system briefing subscriptions. Custom research is not enabled.");
        }

        /// <summary>Check capabilities and retained topic slots at scheduling, worker admission and model execution. Downgrade keeps the oldest slots; archiving a topic releases its slot.</summary>
        public async Task CheckTopicResearchAsync(Topic topic, string userId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(topic);
            await CheckResearchAsync(topic.TenantId, userId, topic.IsOneTime, token).ConfigureAwait(false);
            if (topic.IsOneTime) return;
            EffectivePlan plan = await GetEffectiveAsync(topic.TenantId, userId, token).ConfigureAwait(false);
            long? maximum = (long?)Limit(plan, "customTopics.max") ?? (long?)Limit(plan, "topics.max");
            if (maximum.HasValue && !await _Database.Topics.WithinSlotsAsync(topic, maximum.Value, token).ConfigureAwait(false))
                throw new CapReachedException("customTopics.max", maximum.Value.ToString(CultureInfo.InvariantCulture), null, "This topic is outside the current plan's retained slots. Archive an earlier topic or change your plan to resume it.");
        }

        /// <summary>Early topic-limit refusal before setup or model work. The actual insert or restore also checks capacity atomically.</summary>
        public async Task CheckAvailableTopicSlotAsync(string tenantId, string userId, CancellationToken token = default)
        {
            await CheckResearchAsync(tenantId, userId, false, token).ConfigureAwait(false);
            long total = await _Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId, IsOneTime = false }, token).ConfigureAwait(false);
            long archived = await _Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId, IsOneTime = false, Status = TopicStatusEnum.Archived }, token).ConfigureAwait(false);
            await CheckCountAsync(tenantId, userId, "topics.max", total - archived, "custom topics", null, token).ConfigureAwait(false);
        }

        /// <summary>Resolve the limit used by atomic topic writes. Disabled research returns zero; one-time research has no recurring slot.</summary>
        public async Task<long?> TopicSlotLimitAsync(string tenantId, string userId, bool oneTime, CancellationToken token = default)
        {
            EffectivePlan plan = await GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            if (!Allowed(plan, oneTime ? "oneTimeResearch.enabled" : "customResearch.enabled")) return 0;
            return oneTime ? null : (long?)Limit(plan, "topics.max");
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
        /// Refuse when the user's plan has no audio or this month's audio minutes are used up. Called before audio is
        /// queued, whether someone asked for it or a topic, an edition, or a one-time request makes it automatically.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User the audio is made for.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="CapReachedException">Audio is not in the plan (audio.enabled) or the monthly minutes are used (audio.monthlyMinutes.max).</exception>
        public async Task CheckAudioAsync(string tenantId, string userId, CancellationToken token = default)
        {
            UsageMeter meter = await MeterAsync(tenantId, userId, token).ConfigureAwait(false);
            if (!meter.AudioEnabled) throw new CapReachedException("audio.enabled", "false", null, "Audio is not included in the " + meter.PlanName + " plan.");
            if (meter.AudioMinutesMax.HasValue && meter.AudioMinutes >= meter.AudioMinutesMax.Value)
                throw new CapReachedException("audio.monthlyMinutes.max", meter.AudioMinutesMax.Value.ToString(CultureInfo.InvariantCulture), meter.ResetUtc,
                    "You have used this month's " + meter.AudioMinutesMax.Value + " audio minutes. They reset on " + meter.ResetUtc.ToString("MMMM d", CultureInfo.InvariantCulture) + ".");
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
            AudioUsageSummary dailyAudio = await _Database.AudioReservations.UsageAsync(tenantId, userId, now.Date, token).ConfigureAwait(false);
            return new UsageMeter
            {
                AudioGeneratedSecondsToday = dailyAudio.GeneratedMilliseconds / 1000d,
                AudioReservedSecondsToday = dailyAudio.ReservedSeconds,
                AudioDailySecondsMax = (long?)Limit(plan, "audio.dailySeconds.max"),
                AudioResetUtc = now.Date.AddDays(1),
                SystemSubscriptions = await _Database.CatalogSubscriptions.CountAsync(new CatalogSubscriptionFilter { TenantId = tenantId, UserId = userId, Active = true, Suspended = false }, token).ConfigureAwait(false),
                SystemSubscriptionsMax = (long?)Limit(plan, "systemSubscriptions.max"),
                PlanKey = plan.Plan.Key,
                PlanName = plan.Plan.Name,
                MonthToDateCostUsd = spent,
                MonthlyCostCapUsd = cap,
                CostUsedFraction = cap.HasValue && cap.Value > 0 ? Math.Round((double)(spent / cap.Value), 4) : null,
                ResetUtc = MonthlyReset(now),
                Topics = Math.Max(0,
                    await _Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId, IsOneTime = false }, token).ConfigureAwait(false)
                    - await _Database.Topics.CountAsync(new TopicFilter { TenantId = tenantId, UserId = userId, IsOneTime = false, Status = TopicStatusEnum.Archived }, token).ConfigureAwait(false)),
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
            AccountPlanPolicy? policy = (await _Database.AccountPlanPolicies.EnumerateAsync(new AccountPlanPolicyFilter { TenantId = tenantId, UserId = userId, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
            List<PlanAssignment> all = await AssignmentsAsync(tenantId, userId, token).ConfigureAwait(false);
            List<PlanAssignment> live = all.Where(a => !a.RevokedUtc.HasValue && (!a.EndsUtc.HasValue || a.EndsUtc > now)).ToList();
            PlanAssignment? current = live.Where(a => a.StartsUtc <= now && a.Source != PlanSourceEnum.Billing).OrderByDescending(a => a.Source == PlanSourceEnum.Admin).ThenByDescending(a => a.StartsUtc).ThenByDescending(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            Plan? plan = current == null ? null : await _Database.Plans.ReadAsync(current.PlanId, token).ConfigureAwait(false);
            BillingSubscription? billing = null;
            if (current?.Source != PlanSourceEnum.Admin)
            {
                List<BillingSubscription> paid = (await _Database.BillingSubscriptions.EnumerateAsync(new BillingSubscriptionFilter { TenantId = tenantId, UserId = userId, Active = true, PageSize = 1000 }, token).ConfigureAwait(false)).Items;
                List<Plan> packages = (await _Database.Plans.EnumerateAsync(new PlanFilter { PageSize = 1000 }, token).ConfigureAwait(false)).Items;
                billing = paid.Where(b => MyMorningNews.Core.Billing.BillingService.Entitled(b, now) && (b.Environment == "Production" || _Settings.Billing.AllowSandbox))
                    .OrderByDescending(b => packages.FirstOrDefault(p => p.Id == b.PlanId)?.SortOrder ?? 0).ThenByDescending(b => b.LastVerifiedUtc).FirstOrDefault();
                if (billing != null)
                {
                    plan = packages.FirstOrDefault(p => p.Id == billing.PlanId);
                    current = null;
                    SignupBenefit? benefit = (await _Database.SignupBenefits.EnumerateAsync(new SignupBenefitFilter { TenantId = tenantId, UserId = userId, Active = true, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
                    if (benefit != null && !benefit.RevokedUtc.HasValue && benefit.RedeemedSubscriptionId == billing.Id && benefit.RedeemedUtc.HasValue
                        && benefit.BillingPlanKey == plan?.Key && (!benefit.DurationDays.HasValue || benefit.RedeemedUtc.Value.AddDays(benefit.DurationDays.Value) > now))
                    {
                        Plan? custom = await _Database.Plans.ReadAsync(benefit.PlanId, token).ConfigureAwait(false);
                        if (custom?.Active == true) plan = custom;
                    }
                }
            }
            if (plan == null)
            {
                current = null;
                plan = policy == null ? await DefaultPlanAsync(token).ConfigureAwait(false)
                    : await _Database.Plans.ReadAsync(policy.DefaultPlanId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("The account's default plan is unavailable.");
            }

            if (current?.Source != PlanSourceEnum.Admin && billing == null && policy?.FundingMode == "Personal")
                plan = (await _Database.Plans.EnumerateAsync(new PlanFilter { Key = "byok", Active = true, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault()
                    ?? throw new InvalidOperationException("The BYOK package is unavailable.");

            Dictionary<string, string> features = (await _Database.PlanFeatures.EnumerateAsync(new PlanFeatureFilter { PlanId = plan.Id, PageSize = 500 }, token).ConfigureAwait(false)).Items
                .ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
            EffectivePlan result = new EffectivePlan { Plan = plan, Features = features, Assignment = current, EndsUtc = billing?.EndsUtc ?? current?.EndsUtc, BillingSubscriptionId = billing?.Id, BillingProvider = billing?.Provider, Upcoming = live.Where(a => a.StartsUtc > now).OrderBy(a => a.StartsUtc).ToList() };
            result.SubscriptionRequired = policy?.SubscriptionRequired ?? _Settings.Hosting.DefaultPlanKey != "unlimited";
            result.FundingMode = plan.Key == "byok" ? "Personal" : billing != null ? "Platform" : policy?.FundingMode == "SelfHosted" ? "SelfHosted" : policy == null && _Settings.Hosting.DefaultPlanKey == "unlimited" ? "SelfHosted" : "Platform";
            foreach (string key in features.Keys) result.FeatureSources[key] = "plan";
            List<EntitlementOverride> overrides = (await _Database.EntitlementOverrides.EnumerateAsync(new EntitlementOverrideFilter { TenantId = tenantId, UserId = userId, Active = true, PageSize = 500 }, token).ConfigureAwait(false)).Items;
            foreach (EntitlementOverride item in overrides.Where(o => !o.ExpiresUtc.HasValue || o.ExpiresUtc > now).OrderBy(o => o.LastUpdateUtc).ThenBy(o => o.FeatureKey == "customTopics.max"))
            {
                result.Features[item.FeatureKey] = PlanFeatureCatalog.Normalize(item.FeatureKey, item.Value);
                result.FeatureSources[item.FeatureKey] = "userOverride";
                if (item.FeatureKey == "customTopics.max") { result.Features["topics.max"] = result.Features[item.FeatureKey]; result.FeatureSources["topics.max"] = "userOverride"; }
                if (item.FeatureKey == "topics.max") { result.Features["customTopics.max"] = result.Features[item.FeatureKey]; result.FeatureSources["customTopics.max"] = "userOverride"; }
            }
            result.PlanNames[plan.Id] = plan.Name;
            foreach (string id in result.Upcoming.Select(a => a.PlanId).Distinct().Where(id => !result.PlanNames.ContainsKey(id)))
            {
                Plan? p = await _Database.Plans.ReadAsync(id, token).ConfigureAwait(false);
                if (p != null) result.PlanNames[id] = p.Name;
            }

            return result;
        }

        /// <summary>
        /// Capture the current signup policy and fallback tier for a newly created user. Existing policies are retained.
        /// </summary>
        /// <param name="tenantId">User's tenant.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Captured policy.</returns>
        /// <exception cref="InvalidOperationException">The configured tier is missing or disabled.</exception>
        public async Task<AccountPlanPolicy> CaptureSignupPolicyAsync(string tenantId, string userId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            AccountPlanPolicy? existing = (await _Database.AccountPlanPolicies.EnumerateAsync(new AccountPlanPolicyFilter { TenantId = tenantId, UserId = userId, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault();
            if (existing != null) return existing;
            Plan plan = await SignupPlanAsync(token).ConfigureAwait(false);
            string key = plan.Key;
            return await _Database.AccountPlanPolicies.CreateAsync(new AccountPlanPolicy
            {
                TenantId = tenantId, UserId = userId, DefaultPlanId = plan.Id,
                SubscriptionRequired = _Settings.Hosting.NewUsersRequireSubscription && key != "unlimited" && key != "self-hosted",
                FundingMode = key == "unlimited" || key == "self-hosted" ? "SelfHosted" : "Platform"
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolve and validate the signup fallback before creating an account, preventing incomplete accounts when configuration is invalid.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Active configured plan.</returns>
        /// <exception cref="InvalidOperationException">Configured plan is unavailable.</exception>
        public async Task<Plan> SignupPlanAsync(CancellationToken token = default)
        {
            string key = _Settings.Hosting.NewUsersRequireSubscription ? _Settings.Hosting.DefaultPlanKey : _Settings.Hosting.NonBilledPlanKey;
            return (await _Database.Plans.EnumerateAsync(new PlanFilter { Key = key, Active = true, PageSize = 1 }, token).ConfigureAwait(false)).Items.FirstOrDefault()
                ?? throw new InvalidOperationException("The configured signup plan is unavailable: " + key + ".");
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
