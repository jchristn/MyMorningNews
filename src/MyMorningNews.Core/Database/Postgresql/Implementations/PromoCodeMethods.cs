namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IPromoCodeMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class PromoCodeMethods : PostgresqlMethodsBase, IPromoCodeMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "redemptionCount", "redemptioncount" },
            { "expiresUtc", "expiresutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public PromoCodeMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<PromoRedemption?> RedeemAsync(string tenantId, string userId, string codeId, PromoChannelEnum channel, bool newAccount, CancellationToken token = default)
        {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("promo:" + tenantId + ":" + userId));
            long key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
            // The account lock is acquired before the statement snapshot. The conditional UPDATE also serializes the global counter.
            string sql = @"WITH claimed AS (
 UPDATE promocodes p SET redemptioncount=redemptioncount+1,lastupdateutc=now()
 WHERE p.id=$3 AND p.active AND p.enabled AND (p.startsutc IS NULL OR p.startsutc<=now())
 AND (p.expiresutc IS NULL OR p.expiresutc>now()) AND (p.maxredemptions IS NULL OR p.redemptioncount<p.maxredemptions)
 AND (NOT p.newaccountsonly OR $5) AND EXISTS(SELECT 1 FROM plans WHERE id=p.planid AND active)
 AND EXISTS(SELECT 1 FROM users WHERE tenantid=$1 AND id=$2 AND active)
 AND (SELECT COUNT(*) FROM promoredemptions WHERE tenantid=$1 AND userid=$2 AND promocodeid=p.id)<p.peruserlimit
 AND (p.benefitkind='Complimentary' OR ($5 AND NOT EXISTS(SELECT 1 FROM signupbenefits WHERE tenantid=$1 AND userid=$2)))
 RETURNING p.*
), timing AS (
 SELECT p.*, COALESCE((SELECT CASE WHEN bool_or(endsutc IS NULL) THEN now() ELSE GREATEST(now(),MAX(endsutc)) END
 FROM planassignments WHERE tenantid=$1 AND userid=$2 AND planid=p.planid AND active AND revokedutc IS NULL
 AND (endsutc IS NULL OR endsutc>now())),now()) AS beginutc FROM claimed p
), granted AS (
 INSERT INTO planassignments (" + PlanAssignmentQueries.Columns + @")
 SELECT $6,$1,$2,planid,'Promo',id,beginutc,CASE WHEN durationdays IS NULL THEN NULL ELSE beginutc+durationdays*INTERVAL '1 day' END,
 NULL,'Promotional code '||code||'.',NULL,TRUE,now(),now() FROM timing WHERE benefitkind='Complimentary' RETURNING id
), benefit AS (
 INSERT INTO signupbenefits (" + SignupBenefitQueries.Columns + @")
 SELECT $7,$1,$2,id,planid,discountpercent,discountmonths,NULL,NULL,TRUE,now(),now(),billingplankey,durationdays,NULL,appleofferid,googleofferid
 FROM claimed WHERE benefitkind='PaidOffer' RETURNING id
)
 INSERT INTO promoredemptions (" + PromoRedemptionQueries.Columns + @")
 SELECT $8,$1,p.id,p.code,$2,(SELECT email FROM users WHERE tenantid=$1 AND id=$2),(SELECT id FROM granted),$4,NULL,TRUE,now(),now(),(SELECT id FROM benefit)
 FROM claimed p RETURNING " + PromoRedemptionQueries.Columns;
            List<PromoRedemption> rows = await Driver.QueryWithAdvisoryLockAsync(key, sql, PromoRedemptionMethods.Map,
                new object?[] { tenantId, userId, codeId, channel.ToString(), newAccount, IdGenerator.GeneratePlanAssignmentId(), IdGenerator.GenerateSignupBenefitId(), IdGenerator.GeneratePromoRedemptionId() }, token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode> CreateAsync(PromoCode obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GeneratePromoCodeId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(PromoCodeQueries.Insert, token,
                obj.Id,
                obj.Code,
                obj.Description,
                obj.PlanId,
                obj.DurationDays,
                obj.MaxRedemptions,
                obj.RedemptionCount,
                obj.PerUserLimit,
                obj.StartsUtc.HasValue ? DateTime.SpecifyKind(obj.StartsUtc.Value, DateTimeKind.Utc) : null,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Enabled,
                obj.NewAccountsOnly,
                obj.BatchId,
                obj.CreatedByUserId,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc), obj.BenefitKind, obj.BillingPlanKey, obj.DiscountPercent, obj.DiscountMonths, obj.AppleOfferId, obj.GoogleOfferId).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<PromoCode?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<PromoCode> rows = await QueryAsync(PromoCodeQueries.SelectById, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode> UpdateAsync(PromoCode obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(PromoCodeQueries.Update, token,
                obj.Id,
                obj.Code,
                obj.Description,
                obj.PlanId,
                obj.DurationDays,
                obj.MaxRedemptions,
                obj.RedemptionCount,
                obj.PerUserLimit,
                obj.StartsUtc.HasValue ? DateTime.SpecifyKind(obj.StartsUtc.Value, DateTimeKind.Utc) : null,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Enabled,
                obj.NewAccountsOnly,
                obj.BatchId,
                obj.CreatedByUserId,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc), obj.BenefitKind, obj.BillingPlanKey, obj.DiscountPercent, obj.DiscountMonths, obj.AppleOfferId, obj.GoogleOfferId).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("PromoCode " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(PromoCodeQueries.Delete, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(PromoCodeQueries.Exists, r => 1, token, id).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<PromoCode>> EnumerateAsync(PromoCodeFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + PromoCodeQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + PromoCodeQueries.Columns + " FROM " + PromoCodeQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<PromoCode> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<PromoCode>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(PromoCodeFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + PromoCodeQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode?> ReadByCodeAsync(string code, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(code)) return null;
            List<PromoCode> rows = await QueryAsync("SELECT " + PromoCodeQueries.Columns + " FROM promocodes WHERE code = $1 LIMIT 1", Map, token, code.Trim().ToUpperInvariant()).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<PromoCode?> TryClaimAsync(string id, DateTime nowUtc, CancellationToken token = default)
        {
            string sql = "UPDATE promocodes SET redemptioncount = redemptioncount + 1, lastupdateutc = $2 WHERE id = $1 AND enabled = TRUE AND active = TRUE " +
                "AND (startsutc IS NULL OR startsutc <= $2) AND (expiresutc IS NULL OR expiresutc > $2) AND (maxredemptions IS NULL OR redemptioncount < maxredemptions) RETURNING " + PromoCodeQueries.Columns;
            List<PromoCode> rows = await QueryAsync(sql, Map, token, id, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(string id, CancellationToken token = default)
        {
            await ExecuteAsync("UPDATE promocodes SET redemptioncount = GREATEST(0, redemptioncount - 1), lastupdateutc = $2 WHERE id = $1", token, id, DateTime.UtcNow).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="PromoCodeQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static PromoCode Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            PromoCode obj = new PromoCode();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.Code = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.Description = (reader.IsDBNull(2) ? null : reader.GetString(2));
            obj.PlanId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.DurationDays = (reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4));
            obj.MaxRedemptions = (reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5));
            obj.RedemptionCount = reader.GetInt32(6);
            obj.PerUserLimit = reader.GetInt32(7);
            obj.StartsUtc = (reader.IsDBNull(8) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc));
            obj.ExpiresUtc = (reader.IsDBNull(9) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
            obj.Enabled = reader.GetBoolean(10);
            obj.NewAccountsOnly = reader.GetBoolean(11);
            obj.BatchId = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.CreatedByUserId = (reader.IsDBNull(13) ? null : reader.GetString(13));
            obj.Active = reader.GetBoolean(14);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            obj.BenefitKind = reader.GetString(17);
            obj.BillingPlanKey = reader.IsDBNull(18) ? null : reader.GetString(18);
            obj.DiscountPercent = reader.GetDecimal(19);
            obj.DiscountMonths = reader.IsDBNull(20) ? null : reader.GetInt32(20);
            obj.AppleOfferId = reader.IsDBNull(21) ? null : reader.GetString(21);
            obj.GoogleOfferId = reader.IsDBNull(22) ? null : reader.GetString(22);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(PromoCodeFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (!String.IsNullOrWhiteSpace(filter.Code))
            {
                args.Add(filter.Code);
                clauses.Add("code = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.PlanId))
            {
                args.Add(filter.PlanId);
                clauses.Add("planid = $" + args.Count);
            }
            if (filter.Enabled.HasValue)
            {
                args.Add(filter.Enabled.Value);
                clauses.Add("enabled = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.BatchId))
            {
                args.Add(filter.BatchId);
                clauses.Add("batchid = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(code ILIKE $" + args.Count + " OR description ILIKE $" + args.Count + ")");
            }
            if (filter.FromUtc.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.FromUtc.Value, DateTimeKind.Utc));
                clauses.Add("createdutc >= $" + args.Count);
            }
            if (filter.ToUtc.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.ToUtc.Value, DateTimeKind.Utc));
                clauses.Add("createdutc < $" + args.Count);
            }
            return clauses.Count > 0 ? " WHERE " + String.Join(" AND ", clauses) : String.Empty;
        }

        #endregion
    }
}
