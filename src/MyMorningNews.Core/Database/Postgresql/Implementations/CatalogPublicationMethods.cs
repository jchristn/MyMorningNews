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
    /// PostgreSQL implementation of <see cref="ICatalogPublicationMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class CatalogPublicationMethods : PostgresqlMethodsBase, ICatalogPublicationMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "sortOrder", "sortorder" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public CatalogPublicationMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<bool> CanGenerateTopicAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            List<bool> rows = await QueryAsync(@"SELECT NOT EXISTS (SELECT 1 FROM catalogowner WHERE tenantid=$1)
OR EXISTS (SELECT 1 FROM catalogpublications WHERE ownertenantid=$1 AND topicid=$2 AND active AND status='Published')", r => r.GetBoolean(0), token, tenantId, topicId).ConfigureAwait(false);
            return rows[0];
        }

        /// <inheritdoc />
        public async Task<User> EnsureOwnerAsync(CancellationToken token = default)
        {
            string tenantId = IdGenerator.GenerateTenantId();
            string userId = IdGenerator.GenerateUserId();
            const string sql = @"WITH existing AS (SELECT tenantid, userid FROM catalogowner WHERE singleton = TRUE),
newtenant AS (
 INSERT INTO tenants (id, name, description, timezone, isprotected, active, createdutc, lastupdateutc, ispersonal)
 SELECT $1, 'System briefings', 'Protected canonical catalog workspace', 'UTC', TRUE, TRUE, now(), now(), FALSE
 WHERE NOT EXISTS (SELECT 1 FROM existing) RETURNING id),
newuser AS (
 INSERT INTO users (id, tenantid, email, passwordsha256, isadmin, istenantadmin, isprotected, active, createdutc, lastupdateutc, selfsignup)
 SELECT $2, id, $3, $4, FALSE, FALSE, TRUE, TRUE, now(), now(), FALSE FROM newtenant RETURNING id, tenantid),
owner AS (
 INSERT INTO catalogowner (singleton, tenantid, userid) SELECT TRUE, tenantid, id FROM newuser RETURNING tenantid, userid)
 , allowners AS (SELECT tenantid, userid FROM owner UNION ALL SELECT tenantid, userid FROM existing),
policy AS (
 INSERT INTO accountplanpolicies (id, tenantid, userid, subscriptionrequired, defaultplanid, fundingmode, active, createdutc, lastupdateutc)
 SELECT $5, o.tenantid, o.userid, FALSE, p.id, 'Platform', TRUE, now(), now() FROM allowners o CROSS JOIN plans p WHERE p.key = 'self-hosted'
 ON CONFLICT (tenantid, userid) DO NOTHING)
SELECT tenantid, userid FROM allowners";
            List<string[]> owners = await Driver.QueryWithAdvisoryLockAsync(727713901463L, sql,
                r => new[] { r.GetString(0), r.GetString(1) },
                new object?[] { tenantId, userId, userId + "@catalog.invalid", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), IdGenerator.GenerateAccountPlanPolicyId() }, token).ConfigureAwait(false);
            return await Driver.Users.ReadAsync(owners[0][0], owners[0][1], token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The catalog service owner is missing.");
        }

        /// <inheritdoc />
        public async Task<CatalogPublication> CreateAsync(CatalogPublication obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateCatalogPublicationId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(CatalogPublicationQueries.Insert, token,
                obj.Id,
                obj.TopicId,
                obj.OwnerTenantId,
                obj.Title,
                obj.Description,
                obj.Category,
                obj.Language,
                obj.Status,
                obj.FreeEligible,
                obj.SortOrder,
                obj.Revision,
                obj.PublishedUtc.HasValue ? DateTime.SpecifyKind(obj.PublishedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<CatalogPublication?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<CatalogPublication> rows = await QueryAsync(CatalogPublicationQueries.SelectById, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<CatalogPublication> UpdateAsync(CatalogPublication obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(CatalogPublicationQueries.Update, token,
                obj.Id,
                obj.TopicId,
                obj.OwnerTenantId,
                obj.Title,
                obj.Description,
                obj.Category,
                obj.Language,
                obj.Status,
                obj.FreeEligible,
                obj.SortOrder,
                obj.Revision,
                obj.PublishedUtc.HasValue ? DateTime.SpecifyKind(obj.PublishedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("CatalogPublication " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(CatalogPublicationQueries.Delete, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(CatalogPublicationQueries.Exists, r => 1, token, id).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<CatalogPublication>> EnumerateAsync(CatalogPublicationFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + CatalogPublicationQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + CatalogPublicationQueries.Columns + " FROM " + CatalogPublicationQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<CatalogPublication> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<CatalogPublication>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(CatalogPublicationFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + CatalogPublicationQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="CatalogPublicationQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static CatalogPublication Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            CatalogPublication obj = new CatalogPublication();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TopicId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.OwnerTenantId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Title = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Description = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.Category = (reader.IsDBNull(5) ? String.Empty : reader.GetString(5));
            obj.Language = (reader.IsDBNull(6) ? String.Empty : reader.GetString(6));
            obj.Status = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.FreeEligible = reader.GetBoolean(8);
            obj.SortOrder = reader.GetInt32(9);
            obj.Revision = reader.GetInt64(10);
            obj.PublishedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(12);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(CatalogPublicationFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (!String.IsNullOrWhiteSpace(filter.Category))
            {
                args.Add(filter.Category);
                clauses.Add("category = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Language))
            {
                args.Add(filter.Language);
                clauses.Add("language = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Status))
            {
                args.Add(filter.Status);
                clauses.Add("status = $" + args.Count);
            }
            if (filter.FreeEligible.HasValue)
            {
                args.Add(filter.FreeEligible.Value);
                clauses.Add("freeeligible = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(title ILIKE $" + args.Count + " OR description ILIKE $" + args.Count + ")");
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
