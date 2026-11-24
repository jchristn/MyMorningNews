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
    /// PostgreSQL implementation of <see cref="ISourceItemMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class SourceItemMethods : PostgresqlMethodsBase, ISourceItemMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "fetchedUtc", "fetchedutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public SourceItemMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<SourceItem> CreateAsync(SourceItem obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateSourceItemId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(SourceItemQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.CanonicalUrl,
                obj.CanonicalUrlHash,
                obj.Domain,
                obj.Title,
                obj.PublishedUtc.HasValue ? DateTime.SpecifyKind(obj.PublishedUtc.Value, DateTimeKind.Utc) : null,
                DateTime.SpecifyKind(obj.FetchedUtc, DateTimeKind.Utc),
                obj.HttpStatus,
                obj.ContentType,
                obj.ContentHash,
                obj.ContentText,
                obj.ContentBytes,
                obj.FirstSeenRunId,
                obj.TimesSeen,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<SourceItem?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<SourceItem> rows = await QueryAsync(SourceItemQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<SourceItem?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<SourceItem> rows = await QueryAsync(SourceItemQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<SourceItem> UpdateAsync(SourceItem obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(SourceItemQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.CanonicalUrl,
                obj.CanonicalUrlHash,
                obj.Domain,
                obj.Title,
                obj.PublishedUtc.HasValue ? DateTime.SpecifyKind(obj.PublishedUtc.Value, DateTimeKind.Utc) : null,
                DateTime.SpecifyKind(obj.FetchedUtc, DateTimeKind.Utc),
                obj.HttpStatus,
                obj.ContentType,
                obj.ContentHash,
                obj.ContentText,
                obj.ContentBytes,
                obj.FirstSeenRunId,
                obj.TimesSeen,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("SourceItem " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(SourceItemQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(SourceItemQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(SourceItemQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<SourceItem>> EnumerateAsync(SourceItemFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + SourceItemQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + SourceItemQueries.Columns + " FROM " + SourceItemQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<SourceItem> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<SourceItem>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(SourceItemFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + SourceItemQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> IsVisibleToUserAsync(string tenantId, string userId, string sourceItemId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            if (String.IsNullOrWhiteSpace(sourceItemId)) throw new ArgumentNullException(nameof(sourceItemId));
            List<long> rows = await QueryAsync("SELECT COUNT(*) FROM searchresults r WHERE r.tenantid = $1 AND r.sourceitemid = $2 AND r.topicid IN (SELECT id FROM topics WHERE tenantid = $1 AND userid = $3)",
                r => r.GetInt64(0), token, tenantId, sourceItemId, userId).ConfigureAwait(false);
            return rows.FirstOrDefault() > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<SourceItem>> ListForTopicsAsync(string tenantId, string? userId, string? topicId, string? runId, string? search, int pageNumber, int pageSize, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?> { tenantId };
            List<string> inner = new List<string> { "r.tenantid = $1", "r.sourceitemid IS NOT NULL" };
            if (!String.IsNullOrWhiteSpace(userId))
            {
                args.Add(userId);
                inner.Add("r.topicid IN (SELECT id FROM topics WHERE tenantid = $1 AND userid = $" + args.Count + ")");
            }
            if (!String.IsNullOrWhiteSpace(topicId))
            {
                args.Add(topicId);
                inner.Add("r.topicid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(runId))
            {
                args.Add(runId);
                inner.Add("r.runid = $" + args.Count);
            }
            string where = "s.tenantid = $1 AND s.id IN (SELECT r.sourceitemid FROM searchresults r WHERE " + String.Join(" AND ", inner) + ")";
            if (!String.IsNullOrWhiteSpace(search))
            {
                args.Add("%" + EscapeLike(search.Trim()) + "%");
                where += " AND (s.title ILIKE $" + args.Count + " OR s.domain ILIKE $" + args.Count + " OR s.canonicalurl ILIKE $" + args.Count + ")";
            }
            List<long> total = await QueryAsync("SELECT COUNT(*) FROM sourceitems s WHERE " + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            int size = Math.Clamp(pageSize, 1, 1000);
            int page = Math.Max(1, pageNumber);
            args.Add(size);
            args.Add((page - 1) * size);
            string cols = String.Join(", ", SourceItemQueries.Columns.Split(',').Select(c => "s." + c.Trim()));
            List<SourceItem> items = await QueryAsync("SELECT " + cols + " FROM sourceitems s WHERE " + where + " ORDER BY s.createdutc DESC, s.id DESC LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count, Map, token, args.ToArray()).ConfigureAwait(false);
            foreach (SourceItem i in items) i.ContentText = null;
            long count = total.FirstOrDefault();
            return new EnumerationResult<SourceItem> { Objects = items, PageNumber = page, PageSize = size, TotalRecords = count, TotalPages = count == 0 ? 0 : (int)Math.Ceiling(count / (double)size), TotalMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2) };
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="SourceItemQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static SourceItem Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            SourceItem obj = new SourceItem();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.CanonicalUrl = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.CanonicalUrlHash = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Domain = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.Title = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.PublishedUtc = (reader.IsDBNull(6) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc));
            obj.FetchedUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);
            obj.HttpStatus = (reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8));
            obj.ContentType = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.ContentHash = (reader.IsDBNull(10) ? null : reader.GetString(10));
            obj.ContentText = (reader.IsDBNull(11) ? null : reader.GetString(11));
            obj.ContentBytes = reader.GetInt64(12);
            obj.FirstSeenRunId = (reader.IsDBNull(13) ? null : reader.GetString(13));
            obj.TimesSeen = reader.GetInt32(14);
            obj.Active = reader.GetBoolean(15);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(17), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(SourceItemFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.CanonicalUrlHash))
            {
                args.Add(filter.CanonicalUrlHash);
                clauses.Add("canonicalurlhash = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Domain))
            {
                args.Add(filter.Domain);
                clauses.Add("domain = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(title ILIKE $" + args.Count + ")");
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
