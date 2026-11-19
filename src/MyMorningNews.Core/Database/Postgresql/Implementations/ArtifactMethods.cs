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
    /// PostgreSQL implementation of <see cref="IArtifactMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class ArtifactMethods : PostgresqlMethodsBase, IArtifactMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
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
        public ArtifactMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Artifact> CreateAsync(Artifact obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateArtifactId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(ArtifactQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Kind.ToString(),
                obj.TopicId,
                obj.BriefingId,
                obj.ResearchRequestId,
                obj.FileName,
                obj.ContentType,
                obj.Bytes,
                obj.Sha256,
                obj.StorageProvider.ToString(),
                obj.StorageKey,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Pinned,
                obj.DeletedUtc.HasValue ? DateTime.SpecifyKind(obj.DeletedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Artifact?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Artifact> rows = await QueryAsync(ArtifactQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Artifact?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Artifact> rows = await QueryAsync(ArtifactQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Artifact> UpdateAsync(Artifact obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(ArtifactQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Kind.ToString(),
                obj.TopicId,
                obj.BriefingId,
                obj.ResearchRequestId,
                obj.FileName,
                obj.ContentType,
                obj.Bytes,
                obj.Sha256,
                obj.StorageProvider.ToString(),
                obj.StorageKey,
                obj.ExpiresUtc.HasValue ? DateTime.SpecifyKind(obj.ExpiresUtc.Value, DateTimeKind.Utc) : null,
                obj.Pinned,
                obj.DeletedUtc.HasValue ? DateTime.SpecifyKind(obj.DeletedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Artifact " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ArtifactQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(ArtifactQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ArtifactQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Artifact>> EnumerateAsync(ArtifactFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ArtifactQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + ArtifactQueries.Columns + " FROM " + ArtifactQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Artifact> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Artifact>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(ArtifactFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ArtifactQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<Artifact>> ListExpiredAsync(DateTime nowUtc, int limit, CancellationToken token = default)
        {
            string sql = "SELECT " + ArtifactQueries.Columns + " FROM artifacts WHERE pinned = FALSE AND deletedutc IS NULL AND expiresutc IS NOT NULL AND expiresutc <= $1 ORDER BY expiresutc LIMIT $2";
            return await QueryAsync(sql, Map, token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 1000)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> SetPinnedForBriefingAsync(string tenantId, string briefingId, bool pinned, CancellationToken token = default)
        {
            return await ExecuteAsync("UPDATE artifacts SET pinned = $3, lastupdateutc = $4 WHERE tenantid = $1 AND briefingid = $2 AND deletedutc IS NULL", token, tenantId, briefingId, pinned, DateTime.UtcNow).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="ArtifactQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Artifact Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Artifact obj = new Artifact();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Kind = Enum.Parse<ArtifactKindEnum>(reader.GetString(3));
            obj.TopicId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.BriefingId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.ResearchRequestId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.FileName = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.ContentType = (reader.IsDBNull(8) ? String.Empty : reader.GetString(8));
            obj.Bytes = reader.GetInt64(9);
            obj.Sha256 = (reader.IsDBNull(10) ? String.Empty : reader.GetString(10));
            obj.StorageProvider = Enum.Parse<StorageProviderEnum>(reader.GetString(11));
            obj.StorageKey = (reader.IsDBNull(12) ? String.Empty : reader.GetString(12));
            obj.ExpiresUtc = (reader.IsDBNull(13) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc));
            obj.Pinned = reader.GetBoolean(14);
            obj.DeletedUtc = (reader.IsDBNull(15) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(16);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(17), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(18), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(ArtifactFilter filter, List<object?> args)
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
            if (filter.Kind.HasValue)
            {
                args.Add(filter.Kind.Value.ToString());
                clauses.Add("kind = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.BriefingId))
            {
                args.Add(filter.BriefingId);
                clauses.Add("briefingid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.ResearchRequestId))
            {
                args.Add(filter.ResearchRequestId);
                clauses.Add("researchrequestid = $" + args.Count);
            }
            if (filter.StorageProvider.HasValue)
            {
                args.Add(filter.StorageProvider.Value.ToString());
                clauses.Add("storageprovider = $" + args.Count);
            }
            if (filter.Pinned.HasValue)
            {
                args.Add(filter.Pinned.Value);
                clauses.Add("pinned = $" + args.Count);
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
