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
    /// PostgreSQL implementation of <see cref="IBillingSubscriptionMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class BillingSubscriptionMethods : PostgresqlMethodsBase, IBillingSubscriptionMethods
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
        public BillingSubscriptionMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<List<BillingSubscription>> StripeCustomerAsync(string environment, string customerId, CancellationToken token = default)
        {
            return QueryAsync("SELECT " + BillingSubscriptionQueries.Columns + " FROM billingsubscriptions WHERE provider='Stripe' AND environment=$1 AND customerid=$2", Map, token, environment, customerId);
        }

        /// <inheritdoc />
        public async Task<BillingSubscription?> ReadExternalAsync(string provider, string environment, string providerId, CancellationToken token = default)
        {
            List<BillingSubscription> rows = await QueryAsync("SELECT " + BillingSubscriptionQueries.Columns + " FROM billingsubscriptions WHERE provider=$1 AND environment=$2 AND providerid=$3", Map, token, provider, environment, providerId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public Task<List<BillingSubscription>> DueAsync(DateTime cutoffUtc, CancellationToken token = default)
        {
            return QueryAsync("SELECT " + BillingSubscriptionQueries.Columns + " FROM billingsubscriptions WHERE active AND lastverifiedutc < $1 ORDER BY lastverifiedutc LIMIT 50", Map, token, cutoffUtc);
        }
        /// <inheritdoc />
        public Task<BillingSubscription?> SaveVerifiedAsync(BillingSubscription subscription, CancellationToken token = default)
        {
            return SaveReplacementAsync(subscription, null, token);
        }

        /// <inheritdoc />
        public async Task<BillingSubscription?> SaveReplacementAsync(BillingSubscription subscription, string? replacedProviderId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            if (subscription.ProviderId == replacedProviderId) throw new InvalidOperationException("A subscription cannot replace itself.");
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("verified-billing:" + subscription.Provider + ":" + subscription.Environment));
            long key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
            // Provider-scoped serialization makes the replacement ownership check and both state changes indivisible.
            string sql = @"WITH saved AS (
INSERT INTO billingsubscriptions (" + BillingSubscriptionQueries.Columns + @")
SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,TRUE,now(),now(),$13
WHERE NOT EXISTS(SELECT 1 FROM billingsubscriptions WHERE provider=$4 AND environment=$5 AND providerid=$14 AND (tenantid<>$2 OR userid<>$3))
ON CONFLICT (provider,environment,providerid) DO UPDATE SET customerid=EXCLUDED.customerid,planid=EXCLUDED.planid,status=EXCLUDED.status,
 endsutc=EXCLUDED.endsutc,cancelatperiodend=EXCLUDED.cancelatperiodend,lastverifiedutc=EXCLUDED.lastverifiedutc,lastupdateutc=now(),
 verificationsecretencrypted=COALESCE(EXCLUDED.verificationsecretencrypted,billingsubscriptions.verificationsecretencrypted)
WHERE billingsubscriptions.tenantid=EXCLUDED.tenantid AND billingsubscriptions.userid=EXCLUDED.userid
 AND billingsubscriptions.lastverifiedutc <= EXCLUDED.lastverifiedutc AND billingsubscriptions.supersededbyid IS NULL
RETURNING " + BillingSubscriptionQueries.Columns + @"
), replaced AS (
UPDATE billingsubscriptions SET status='Expired',supersededbyid=(SELECT id FROM saved),lastupdateutc=now()
WHERE provider=$4 AND environment=$5 AND providerid=$14 AND tenantid=$2 AND userid=$3
 AND EXISTS(SELECT 1 FROM saved WHERE status IN ('Active','Grace','Canceled') AND endsutc>now())
)
SELECT " + BillingSubscriptionQueries.Columns + " FROM saved";
            List<BillingSubscription> rows = await Driver.QueryWithAdvisoryLockAsync(key, sql, Map, new object?[] { subscription.Id, subscription.TenantId, subscription.UserId,
                subscription.Provider, subscription.Environment, subscription.ProviderId, subscription.CustomerId, subscription.PlanId,
                subscription.Status, subscription.EndsUtc, subscription.CancelAtPeriodEnd, subscription.LastVerifiedUtc, subscription.VerificationSecretEncrypted, replacedProviderId }, token).ConfigureAwait(false);
            if (rows.Count > 0) return rows[0];
            List<BillingSubscription> existing = await QueryAsync("SELECT " + BillingSubscriptionQueries.Columns + " FROM billingsubscriptions WHERE provider=$1 AND environment=$2 AND (providerid=$3 OR providerid=$4)", Map, token, subscription.Provider, subscription.Environment, subscription.ProviderId, replacedProviderId).ConfigureAwait(false);
            if (existing.Any(b => b.TenantId != subscription.TenantId || b.UserId != subscription.UserId))
                throw new InvalidOperationException("This purchase or its replaced subscription already belongs to another account.");
            return null;
        }


        /// <inheritdoc />
        public async Task<BillingSubscription> CreateAsync(BillingSubscription obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateBillingSubscriptionId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(BillingSubscriptionQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Provider,
                obj.Environment,
                obj.ProviderId,
                obj.CustomerId,
                obj.PlanId,
                obj.Status,
                obj.EndsUtc.HasValue ? DateTime.SpecifyKind(obj.EndsUtc.Value, DateTimeKind.Utc) : null,
                obj.CancelAtPeriodEnd,
                DateTime.SpecifyKind(obj.LastVerifiedUtc, DateTimeKind.Utc),
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc), obj.VerificationSecretEncrypted).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<BillingSubscription?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<BillingSubscription> rows = await QueryAsync(BillingSubscriptionQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<BillingSubscription?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<BillingSubscription> rows = await QueryAsync(BillingSubscriptionQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<BillingSubscription> UpdateAsync(BillingSubscription obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(BillingSubscriptionQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Provider,
                obj.Environment,
                obj.ProviderId,
                obj.CustomerId,
                obj.PlanId,
                obj.Status,
                obj.EndsUtc.HasValue ? DateTime.SpecifyKind(obj.EndsUtc.Value, DateTimeKind.Utc) : null,
                obj.CancelAtPeriodEnd,
                DateTime.SpecifyKind(obj.LastVerifiedUtc, DateTimeKind.Utc),
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc), obj.VerificationSecretEncrypted).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("BillingSubscription " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BillingSubscriptionQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(BillingSubscriptionQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BillingSubscriptionQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<BillingSubscription>> EnumerateAsync(BillingSubscriptionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BillingSubscriptionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + BillingSubscriptionQueries.Columns + " FROM " + BillingSubscriptionQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<BillingSubscription> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<BillingSubscription>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(BillingSubscriptionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BillingSubscriptionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="BillingSubscriptionQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static BillingSubscription Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            BillingSubscription obj = new BillingSubscription();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Provider = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Environment = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.ProviderId = (reader.IsDBNull(5) ? String.Empty : reader.GetString(5));
            obj.CustomerId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.PlanId = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.Status = (reader.IsDBNull(8) ? String.Empty : reader.GetString(8));
            obj.EndsUtc = (reader.IsDBNull(9) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
            obj.CancelAtPeriodEnd = reader.GetBoolean(10);
            obj.LastVerifiedUtc = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
            obj.Active = reader.GetBoolean(12);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc);
            obj.VerificationSecretEncrypted = reader.IsDBNull(15) ? null : reader.GetString(15);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(BillingSubscriptionFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.Provider))
            {
                args.Add(filter.Provider);
                clauses.Add("provider = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Environment))
            {
                args.Add(filter.Environment);
                clauses.Add("environment = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.ProviderId))
            {
                args.Add(filter.ProviderId);
                clauses.Add("providerid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Status))
            {
                args.Add(filter.Status);
                clauses.Add("status = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (filter.CreatedAfter.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.CreatedAfter.Value, DateTimeKind.Utc));
                clauses.Add("createdutc > $" + args.Count);
            }
            if (filter.CreatedBefore.HasValue)
            {
                args.Add(DateTime.SpecifyKind(filter.CreatedBefore.Value, DateTimeKind.Utc));
                clauses.Add("createdutc < $" + args.Count);
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
