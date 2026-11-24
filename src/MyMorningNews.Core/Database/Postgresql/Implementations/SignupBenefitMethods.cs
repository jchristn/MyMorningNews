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
    /// PostgreSQL implementation of <see cref="ISignupBenefitMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class SignupBenefitMethods : PostgresqlMethodsBase, ISignupBenefitMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public SignupBenefitMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<bool> BindAsync(string tenantId, string userId, string id, string subscriptionId, string billingPlanKey, CancellationToken token = default)
        {
            return await ExecuteAsync(@"UPDATE signupbenefits b SET redeemedsubscriptionid=$4,redeemedutc=COALESCE(redeemedutc,now()),lastupdateutc=now()
WHERE b.id=$3 AND b.tenantid=$1 AND b.userid=$2 AND b.active AND b.revokedutc IS NULL AND b.billingplankey=$5
AND (b.redeemedsubscriptionid IS NULL OR b.redeemedsubscriptionid=$4)
AND EXISTS(SELECT 1 FROM billingsubscriptions s JOIN plans p ON p.id=s.planid WHERE s.id=$4 AND s.tenantid=$1 AND s.userid=$2
 AND p.key=$5 AND s.active AND s.status IN ('Active','Grace','Canceled') AND s.endsutc>now())", token, tenantId, userId, id, subscriptionId, billingPlanKey).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc />
        public async Task<SignupBenefit> CreateAsync(SignupBenefit obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateSignupBenefitId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(SignupBenefitQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.PromoCodeId,
                obj.PlanId,
                obj.DiscountPercent,
                obj.DiscountMonths,
                obj.RedeemedUtc.HasValue ? DateTime.SpecifyKind(obj.RedeemedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc), obj.BillingPlanKey, obj.DurationDays, obj.RedeemedSubscriptionId, obj.AppleOfferId, obj.GoogleOfferId).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<SignupBenefit?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<SignupBenefit> rows = await QueryAsync(SignupBenefitQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<SignupBenefit?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<SignupBenefit> rows = await QueryAsync(SignupBenefitQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<SignupBenefit> UpdateAsync(SignupBenefit obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(SignupBenefitQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.PromoCodeId,
                obj.PlanId,
                obj.DiscountPercent,
                obj.DiscountMonths,
                obj.RedeemedUtc.HasValue ? DateTime.SpecifyKind(obj.RedeemedUtc.Value, DateTimeKind.Utc) : null,
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc), obj.BillingPlanKey, obj.DurationDays, obj.RedeemedSubscriptionId, obj.AppleOfferId, obj.GoogleOfferId).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("SignupBenefit " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(SignupBenefitQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(SignupBenefitQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(SignupBenefitQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<SignupBenefit>> EnumerateAsync(SignupBenefitFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + SignupBenefitQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + SignupBenefitQueries.Columns + " FROM " + SignupBenefitQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<SignupBenefit> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<SignupBenefit>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(SignupBenefitFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + SignupBenefitQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="SignupBenefitQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static SignupBenefit Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            SignupBenefit obj = new SignupBenefit();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.PromoCodeId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.PlanId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.DiscountPercent = reader.GetDecimal(5);
            obj.DiscountMonths = (reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6));
            obj.RedeemedUtc = (reader.IsDBNull(7) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc));
            obj.RevokedUtc = (reader.IsDBNull(8) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(9);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
            obj.BillingPlanKey = reader.GetString(12);
            obj.DurationDays = reader.IsDBNull(13) ? null : reader.GetInt32(13);
            obj.RedeemedSubscriptionId = reader.IsDBNull(14) ? null : reader.GetString(14);
            obj.AppleOfferId = reader.IsDBNull(15) ? null : reader.GetString(15);
            obj.GoogleOfferId = reader.IsDBNull(16) ? null : reader.GetString(16);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(SignupBenefitFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
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
