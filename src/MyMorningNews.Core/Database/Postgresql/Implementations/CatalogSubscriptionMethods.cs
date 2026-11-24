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
    /// PostgreSQL implementation of <see cref="ICatalogSubscriptionMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class CatalogSubscriptionMethods : PostgresqlMethodsBase, ICatalogSubscriptionMethods
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
        public CatalogSubscriptionMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<List<MediaAsset>> FeedMediaAsync(string tenantId, string userId, bool freeOnly, int maximum, string? mediaId = null, CancellationToken token = default)
        {
            string columns = String.Join(",", MediaAssetQueries.Columns.Split(',').Select(c => "m." + c.Trim()));
            return QueryAsync(@"SELECT " + MediaAssetQueries.Columns + @" FROM (
SELECT DISTINCT ON (b.id) " + columns + @" FROM catalogsubscriptions s
JOIN catalogpublications p ON p.id=s.publicationid
JOIN briefings b ON b.tenantid=p.ownertenantid AND b.topicid=p.topicid
JOIN mediaassets m ON m.tenantid=b.tenantid AND m.briefingid=b.id
WHERE s.tenantid=$1 AND s.userid=$2 AND s.active AND NOT s.suspended AND p.active
AND p.status IN ('Published','Retired') AND (NOT $3 OR p.freeeligible)
AND b.active AND b.status='Ready' AND NOT b.isdryrun AND m.active AND m.status='Ready' AND m.artifactid IS NOT NULL
AND ($5::text IS NULL OR m.id=$5)
ORDER BY b.id,m.createdutc DESC,m.id DESC) episodes
ORDER BY COALESCE(completedutc,createdutc) DESC,id DESC LIMIT $4", MediaAssetMethods.Map, token,
                tenantId, userId, freeOnly, Math.Clamp(maximum, 1, 500), mediaId);
        }

        /// <inheritdoc />
        public async Task ReconcileAsync(string tenantId, string userId, long? maximum, bool freeOnly, CancellationToken token = default)
        {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("catalog:" + tenantId + ":" + userId));
            long lockKey = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
            await Driver.QueryWithAdvisoryLockAsync(lockKey, @"WITH eligible AS (
SELECT s.id, row_number() OVER (ORDER BY s.createdutc,s.id) AS slot FROM catalogsubscriptions s
JOIN catalogpublications p ON p.id=s.publicationid WHERE s.tenantid=$1 AND s.userid=$2 AND s.active
AND p.active AND p.status IN ('Published','Retired') AND (NOT $4 OR p.freeeligible)), retained AS (
SELECT id FROM eligible WHERE $3::bigint IS NULL OR slot <= $3)
UPDATE catalogsubscriptions SET suspended = NOT EXISTS (SELECT 1 FROM retained WHERE retained.id=catalogsubscriptions.id), lastupdateutc=now()
WHERE tenantid=$1 AND userid=$2 AND active AND suspended IS DISTINCT FROM (NOT EXISTS (SELECT 1 FROM retained WHERE retained.id=catalogsubscriptions.id))
RETURNING id", r => r.GetString(0), new object?[] { tenantId, userId, maximum, freeOnly }, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<CatalogIssueResponse>> IssuesAsync(string tenantId, string userId, bool freeOnly, EnumerationFilter filter, CancellationToken token = default, bool audioOnly = false)
        {
            ArgumentNullException.ThrowIfNull(filter);
            string from = @" FROM catalogsubscriptions s JOIN catalogpublications p ON p.id = s.publicationid
JOIN briefings b ON b.tenantid = p.ownertenantid AND b.topicid = p.topicid
WHERE s.tenantid = $1 AND s.userid = $2 AND s.active AND NOT s.suspended AND p.active
AND p.status IN ('Published', 'Retired') AND (NOT $3 OR p.freeeligible) AND b.active AND NOT b.isdryrun AND b.status = 'Ready'";
            if (audioOnly) from += " AND EXISTS (SELECT 1 FROM mediaassets m WHERE m.tenantid = b.tenantid AND m.briefingid = b.id AND m.active AND m.status = 'Ready' AND m.artifactid IS NOT NULL)";
            long total = (await QueryAsync("SELECT count(*)" + from, r => r.GetInt64(0), token, tenantId, userId, freeOnly).ConfigureAwait(false))[0];
            List<CatalogIssueResponse> issues = await QueryAsync("SELECT b.id, p.id, b.title, b.createdutc" + from + " ORDER BY b.createdutc DESC, b.id DESC LIMIT $4 OFFSET $5",
                r => new CatalogIssueResponse { Id = r.GetString(0), PublicationId = r.GetString(1), Title = r.GetString(2), CreatedUtc = r.GetDateTime(3) },
                token, tenantId, userId, freeOnly, filter.PageSize, (long)(filter.PageNumber - 1) * filter.PageSize).ConfigureAwait(false);
            return EnumerationResult<CatalogIssueResponse>.Create(issues, filter, total, 0);
        }

        /// <inheritdoc />
        public async Task<CatalogSubscription?> TrySubscribeAsync(CatalogSubscription subscription, long? maximum, bool freeOnly, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("catalog:" + subscription.TenantId + ":" + subscription.UserId));
            long lockKey = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
            string sql = @"WITH publication AS (
 SELECT id FROM catalogpublications WHERE id = $4 AND active AND status = 'Published' AND (NOT $8 OR freeeligible) FOR SHARE),
existing AS (SELECT id FROM catalogsubscriptions WHERE tenantid = $2 AND userid = $3 AND publicationid = $4 AND active AND NOT suspended)
INSERT INTO catalogsubscriptions (" + CatalogSubscriptionQueries.Columns + @")
SELECT $1, $2, $3, publication.id, $5, $6, FALSE, TRUE, now(), now() FROM publication
WHERE EXISTS (SELECT 1 FROM existing) OR $7::bigint IS NULL OR
 (SELECT count(*) FROM catalogsubscriptions WHERE tenantid = $2 AND userid = $3 AND active AND NOT suspended) < $7
ON CONFLICT (tenantid, userid, publicationid) DO UPDATE SET active = TRUE, suspended = FALSE,
 emailenabled = EXCLUDED.emailenabled, pushenabled = EXCLUDED.pushenabled, lastupdateutc = now(),
 createdutc = CASE WHEN catalogsubscriptions.active THEN catalogsubscriptions.createdutc ELSE now() END
RETURNING " + CatalogSubscriptionQueries.Columns;
            List<CatalogSubscription> rows = await Driver.QueryWithAdvisoryLockAsync(lockKey, sql, Map,
                new object?[] { subscription.Id, subscription.TenantId, subscription.UserId, subscription.PublicationId,
                    subscription.EmailEnabled, subscription.PushEnabled, maximum, freeOnly }, token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<CatalogSubscription> CreateAsync(CatalogSubscription obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateCatalogSubscriptionId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(CatalogSubscriptionQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.PublicationId,
                obj.EmailEnabled,
                obj.PushEnabled,
                obj.Suspended,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<CatalogSubscription?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<CatalogSubscription> rows = await QueryAsync(CatalogSubscriptionQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<CatalogSubscription?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<CatalogSubscription> rows = await QueryAsync(CatalogSubscriptionQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<CatalogSubscription> UpdateAsync(CatalogSubscription obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(CatalogSubscriptionQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.PublicationId,
                obj.EmailEnabled,
                obj.PushEnabled,
                obj.Suspended,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("CatalogSubscription " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(CatalogSubscriptionQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(CatalogSubscriptionQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(CatalogSubscriptionQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<CatalogSubscription>> EnumerateAsync(CatalogSubscriptionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + CatalogSubscriptionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + CatalogSubscriptionQueries.Columns + " FROM " + CatalogSubscriptionQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<CatalogSubscription> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<CatalogSubscription>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(CatalogSubscriptionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + CatalogSubscriptionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="CatalogSubscriptionQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static CatalogSubscription Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            CatalogSubscription obj = new CatalogSubscription();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.PublicationId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.EmailEnabled = reader.GetBoolean(4);
            obj.PushEnabled = reader.GetBoolean(5);
            obj.Suspended = reader.GetBoolean(6);
            obj.Active = reader.GetBoolean(7);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(CatalogSubscriptionFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.PublicationId))
            {
                args.Add(filter.PublicationId);
                clauses.Add("publicationid = $" + args.Count);
            }
            if (filter.Suspended.HasValue)
            {
                args.Add(filter.Suspended.Value);
                clauses.Add("suspended = $" + args.Count);
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
