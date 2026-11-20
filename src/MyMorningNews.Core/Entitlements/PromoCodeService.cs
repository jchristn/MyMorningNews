namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Promotional codes: validation, redemption into a plan assignment (stacked after an active one from the same
    /// plan), usage limits enforced atomically, bulk generation, revocation, and usage statistics.
    /// Thread safety: safe for concurrent use; the total limit is enforced in the database.
    /// </summary>
    public class PromoCodeService
    {
        private static readonly Regex _Format = new Regex("^[A-Z0-9][A-Z0-9-]{2,38}[A-Z0-9]$", RegexOptions.Compiled);
        private const string _Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private readonly DatabaseDriverBase _Database;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public PromoCodeService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Normalize a code: trimmed and uppercase.
        /// </summary>
        /// <param name="code">Code.</param>
        /// <returns>Normalized code.</returns>
        public static string Normalize(string? code)
        {
            return (code ?? String.Empty).Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Whether a code has the allowed shape: 4 to 40 uppercase letters, digits, and inner dashes.
        /// </summary>
        /// <param name="code">Normalized code.</param>
        /// <returns>True when valid.</returns>
        public static bool IsWellFormed(string code)
        {
            return _Format.IsMatch(code ?? String.Empty);
        }

        /// <summary>
        /// Check a code without redeeming it.
        /// </summary>
        /// <param name="code">Code.</param>
        /// <param name="tenantId">Tenant of the user who would redeem it, or null (sign-up).</param>
        /// <param name="userId">User who would redeem it, or null (sign-up).</param>
        /// <param name="newAccount">Whether this is at sign-up.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Check.</returns>
        public async Task<PromoCheck> CheckAsync(string? code, string? tenantId, string? userId, bool newAccount, CancellationToken token = default)
        {
            PromoCode? pc = await _Database.PromoCodes.ReadByCodeAsync(Normalize(code), token).ConfigureAwait(false);
            string? reason = await ReasonAsync(pc, tenantId, userId, newAccount, DateTime.UtcNow, token).ConfigureAwait(false);
            Plan? plan = pc == null ? null : await _Database.Plans.ReadAsync(pc.PlanId, token).ConfigureAwait(false);
            return new PromoCheck
            {
                Valid = reason == null,
                Reason = reason,
                PlanName = reason == null ? plan?.Name : null,
                DurationDays = reason == null ? pc?.DurationDays : null,
                Message = Message(reason, plan?.Name, pc?.DurationDays)
            };
        }

        /// <summary>
        /// Redeem a code for a user: count it against the code's limits and create the plan assignment.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="code">Code.</param>
        /// <param name="channel">Where it was redeemed.</param>
        /// <param name="newAccount">Whether this is at sign-up.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Redemption.</returns>
        /// <exception cref="PromoCodeException">The code cannot be redeemed.</exception>
        public async Task<PromoRedemption> RedeemAsync(string tenantId, string userId, string? code, PromoChannelEnum channel, bool newAccount, CancellationToken token = default)
        {
            DateTime now = DateTime.UtcNow;
            string normalized = Normalize(code);
            PromoCode? pc = await _Database.PromoCodes.ReadByCodeAsync(normalized, token).ConfigureAwait(false);
            string? reason = await ReasonAsync(pc, tenantId, userId, newAccount, now, token).ConfigureAwait(false);
            if (reason != null) throw Fail(reason, pc);
            PromoCode claimed = await _Database.PromoCodes.TryClaimAsync(pc!.Id, now, token).ConfigureAwait(false) ?? throw Fail(await ReasonAsync(pc, tenantId, userId, newAccount, now, token).ConfigureAwait(false) ?? "Exhausted", pc);
            try
            {
                User? user = await _Database.Users.ReadAsync(tenantId, userId, token).ConfigureAwait(false);
                PlanAssignment? running = (await _Database.PlanAssignments.EnumerateAsync(new PlanAssignmentFilter { TenantId = tenantId, UserId = userId, PlanId = claimed.PlanId, PageSize = 200 }, token).ConfigureAwait(false)).Items
                    .Where(a => !a.RevokedUtc.HasValue && (!a.EndsUtc.HasValue || a.EndsUtc > now)).OrderByDescending(a => a.EndsUtc ?? DateTime.MaxValue).FirstOrDefault();
                DateTime starts = running?.EndsUtc.HasValue == true ? running.EndsUtc!.Value : now;
                if (running != null && !running.EndsUtc.HasValue) starts = now;
                PlanAssignment assignment = await _Database.PlanAssignments.CreateAsync(new PlanAssignment
                {
                    TenantId = tenantId,
                    UserId = userId,
                    PlanId = claimed.PlanId,
                    Source = PlanSourceEnum.Promo,
                    PromoCodeId = claimed.Id,
                    StartsUtc = starts,
                    EndsUtc = claimed.DurationDays.HasValue ? starts.AddDays(claimed.DurationDays.Value) : null,
                    Note = "Promotional code " + claimed.Code + "."
                }, token).ConfigureAwait(false);
                PromoRedemption r = await _Database.PromoRedemptions.CreateAsync(new PromoRedemption
                {
                    TenantId = tenantId,
                    PromoCodeId = claimed.Id,
                    Code = claimed.Code,
                    UserId = userId,
                    Email = user?.Email,
                    PlanAssignmentId = assignment.Id,
                    Channel = channel
                }, token).ConfigureAwait(false);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.PromoRedemptions, "channel", channel.ToString(), "outcome", "redeemed");
                return r;
            }
            catch
            {
                await _Database.PromoCodes.ReleaseAsync(claimed.Id, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Revoke a redemption: ends its plan assignment now. The redemption still counts toward the code's limit.
        /// </summary>
        /// <param name="redemption">Redemption.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated redemption.</returns>
        public async Task<PromoRedemption> RevokeAsync(PromoRedemption redemption, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(redemption);
            if (redemption.RevokedUtc.HasValue) return redemption;
            PlanAssignment? a = await _Database.PlanAssignments.ReadAsync(redemption.TenantId, redemption.PlanAssignmentId, token).ConfigureAwait(false);
            if (a != null && !a.RevokedUtc.HasValue)
            {
                a.RevokedUtc = DateTime.UtcNow;
                await _Database.PlanAssignments.UpdateAsync(a, token).ConfigureAwait(false);
            }

            redemption.RevokedUtc = DateTime.UtcNow;
            return await _Database.PromoRedemptions.UpdateAsync(redemption, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create codes in bulk from a template: each gets a random suffix after the prefix and shares a batch ID.
        /// </summary>
        /// <param name="template">Settings to copy (plan, duration, limits, dates); its Code is ignored.</param>
        /// <param name="prefix">Prefix such as SPRING (letters and digits, up to 16).</param>
        /// <param name="count">Codes to create (1 to 1000).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created codes.</returns>
        /// <exception cref="ArgumentException">The prefix is malformed.</exception>
        public async Task<List<PromoCode>> GenerateAsync(PromoCode template, string? prefix, int count, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            string p = Normalize(prefix).Trim('-');
            if (p.Length > 16 || !Regex.IsMatch(p, "^[A-Z0-9]*$")) throw new ArgumentException("prefix must be up to 16 letters and digits.", nameof(prefix));
            string batch = "batch_" + IdGenerator.GeneratePromoCodeId().Substring(4);
            List<PromoCode> created = new List<PromoCode>();
            for (int i = 0; i < Math.Clamp(count, 1, 1000); i++)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    string code = (p.Length > 0 ? p + "-" : String.Empty) + Random(8);
                    if (await _Database.PromoCodes.ReadByCodeAsync(code, token).ConfigureAwait(false) != null) continue;
                    created.Add(await _Database.PromoCodes.CreateAsync(new PromoCode
                    {
                        Code = code, Description = template.Description, PlanId = template.PlanId, DurationDays = template.DurationDays, MaxRedemptions = template.MaxRedemptions ?? 1,
                        PerUserLimit = template.PerUserLimit, StartsUtc = template.StartsUtc, ExpiresUtc = template.ExpiresUtc, Enabled = template.Enabled, NewAccountsOnly = template.NewAccountsOnly,
                        BatchId = batch, CreatedByUserId = template.CreatedByUserId
                    }, token).ConfigureAwait(false));
                    break;
                }
            }

            return created;
        }

        /// <summary>
        /// Usage statistics for a code.
        /// </summary>
        /// <param name="pc">Code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Stats.</returns>
        public async Task<PromoCodeStats> StatsAsync(PromoCode pc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(pc);
            DateTime now = DateTime.UtcNow;
            List<PromoRedemption> rs = await RedemptionsAsync(pc, token).ConfigureAwait(false);
            int active = 0;
            foreach (PromoRedemption r in rs.Where(r => !r.RevokedUtc.HasValue))
            {
                PlanAssignment? a = await _Database.PlanAssignments.ReadAsync(r.TenantId, r.PlanAssignmentId, token).ConfigureAwait(false);
                if (a != null && !a.RevokedUtc.HasValue && (!a.EndsUtc.HasValue || a.EndsUtc > now)) active++;
            }

            return new PromoCodeStats
            {
                Code = pc,
                PlanName = (await _Database.Plans.ReadAsync(pc.PlanId, token).ConfigureAwait(false))?.Name,
                Remaining = pc.MaxRedemptions.HasValue ? Math.Max(0, pc.MaxRedemptions.Value - pc.RedemptionCount) : null,
                ActiveAssignments = active,
                Revoked = rs.Count(r => r.RevokedUtc.HasValue),
                LastRedeemedUtc = rs.Count == 0 ? null : rs.Max(r => r.CreatedUtc)
            };
        }

        /// <summary>
        /// All redemptions of a code across tenants, newest first.
        /// </summary>
        /// <param name="pc">Code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Redemptions.</returns>
        public async Task<List<PromoRedemption>> RedemptionsAsync(PromoCode pc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(pc);
            List<PromoRedemption> all = new List<PromoRedemption>();
            int page = 1;
            while (true)
            {
                EnumerationResult<Tenant> tenants = await _Database.Tenants.EnumerateAsync(new TenantFilter { PageNumber = page, PageSize = 200 }, token).ConfigureAwait(false);
                foreach (Tenant t in tenants.Items)
                    all.AddRange((await _Database.PromoRedemptions.EnumerateAsync(new PromoRedemptionFilter { TenantId = t.Id, PromoCodeId = pc.Id, PageSize = 10000 }, token).ConfigureAwait(false)).Items);
                if (page >= tenants.TotalPages) break;
                page++;
            }

            return all.OrderByDescending(r => r.CreatedUtc).ToList();
        }

        private async Task<string?> ReasonAsync(PromoCode? pc, string? tenantId, string? userId, bool newAccount, DateTime now, CancellationToken token)
        {
            if (pc == null || !pc.Active) return "NotFound";
            if (!pc.Enabled) return "Disabled";
            if (pc.StartsUtc.HasValue && pc.StartsUtc > now) return "NotStarted";
            if (pc.ExpiresUtc.HasValue && pc.ExpiresUtc <= now) return "Expired";
            if (pc.MaxRedemptions.HasValue && pc.RedemptionCount >= pc.MaxRedemptions.Value) return "Exhausted";
            if (pc.NewAccountsOnly && !newAccount) return "NewAccountsOnly";
            if (tenantId != null && userId != null)
            {
                long used = await _Database.PromoRedemptions.CountAsync(new PromoRedemptionFilter { TenantId = tenantId, UserId = userId, PromoCodeId = pc.Id }, token).ConfigureAwait(false);
                if (used >= pc.PerUserLimit) return "AlreadyRedeemed";
            }

            return null;
        }

        private static PromoCodeException Fail(string reason, PromoCode? pc)
        {
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.PromoRedemptions, "channel", "any", "outcome", reason);
            return new PromoCodeException(reason, Message(reason, null, pc?.DurationDays));
        }

        private static string Message(string? reason, string? planName, int? days)
        {
            switch (reason)
            {
                case null: return "This code gives you " + (planName ?? "a plan") + (days.HasValue ? " for " + days.Value + " days." : " with no end date.");
                case "NotStarted": return "This code is not active yet.";
                case "Expired": return "This code has expired.";
                case "Exhausted": return "This code has been used the maximum number of times.";
                case "AlreadyRedeemed": return "You have already used this code.";
                case "NewAccountsOnly": return "This code only works when you sign up.";
                default: return "This code is not valid.";
            }
        }

        private static string Random(int length)
        {
            StringBuilder sb = new StringBuilder(length);
            for (int i = 0; i < length; i++) sb.Append(_Alphabet[RandomNumberGenerator.GetInt32(_Alphabet.Length)]);
            return sb.ToString();
        }
    }
}
