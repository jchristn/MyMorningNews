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
    /// PostgreSQL implementation of <see cref="IMediaAssetMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class MediaAssetMethods : PostgresqlMethodsBase, IMediaAssetMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "completedUtc", "completedutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public MediaAssetMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<MediaAsset> CreateAsync(MediaAsset obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateMediaAssetId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(MediaAssetQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.BriefingId,
                obj.EditionId,
                obj.Kind.ToString(),
                obj.Status.ToString(),
                obj.Format,
                obj.VoiceProfileId,
                obj.Title,
                obj.ScriptMarkdown,
                obj.ScriptFromModel,
                obj.ArtifactId,
                obj.ChaptersArtifactId,
                obj.ContentType,
                obj.Bytes,
                obj.DurationSeconds,
                obj.SegmentCount,
                obj.Attempts,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.ErrorMessage,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<MediaAsset?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<MediaAsset> rows = await QueryAsync(MediaAssetQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<MediaAsset?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<MediaAsset> rows = await QueryAsync(MediaAssetQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<MediaAsset> UpdateAsync(MediaAsset obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(MediaAssetQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.BriefingId,
                obj.EditionId,
                obj.Kind.ToString(),
                obj.Status.ToString(),
                obj.Format,
                obj.VoiceProfileId,
                obj.Title,
                obj.ScriptMarkdown,
                obj.ScriptFromModel,
                obj.ArtifactId,
                obj.ChaptersArtifactId,
                obj.ContentType,
                obj.Bytes,
                obj.DurationSeconds,
                obj.SegmentCount,
                obj.Attempts,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.ErrorMessage,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("MediaAsset " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(MediaAssetQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(MediaAssetQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(MediaAssetQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<MediaAsset>> EnumerateAsync(MediaAssetFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + MediaAssetQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + MediaAssetQueries.Columns + " FROM " + MediaAssetQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<MediaAsset> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<MediaAsset>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(MediaAssetFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + MediaAssetQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<MediaAsset>> ClaimQueuedAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, CancellationToken token = default)
        {
            string sql = "UPDATE mediaassets SET status = 'Scripting', lockeduntilutc = $2, attempts = attempts + 1, lastupdateutc = $1 WHERE id IN (SELECT id FROM mediaassets WHERE status = 'Queued' " +
                "ORDER BY createdutc FOR UPDATE SKIP LOCKED LIMIT $3) RETURNING " + MediaAssetQueries.Columns;
            return await QueryAsync(sql, Map, token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), DateTime.SpecifyKind(lockedUntilUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 20)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> RecoverAbandonedAsync(DateTime nowUtc, int maxAttempts, CancellationToken token = default)
        {
            DateTime now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            int failed = await ExecuteAsync("UPDATE mediaassets SET status = 'Failed', errormessage = 'Interrupted too many times.', lockeduntilutc = NULL, lastupdateutc = $1 " +
                "WHERE status IN ('Scripting', 'Synthesizing') AND lockeduntilutc < $1 AND attempts >= $2", token, now, maxAttempts).ConfigureAwait(false);
            int requeued = await ExecuteAsync("UPDATE mediaassets SET status = 'Queued', lockeduntilutc = NULL, lastupdateutc = $1 WHERE status IN ('Scripting', 'Synthesizing') AND lockeduntilutc < $1", token, now).ConfigureAwait(false);
            return failed + requeued;
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="MediaAssetQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static MediaAsset Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            MediaAsset obj = new MediaAsset();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.BriefingId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.EditionId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.Kind = Enum.Parse<MediaKindEnum>(reader.GetString(5));
            obj.Status = Enum.Parse<MediaStatusEnum>(reader.GetString(6));
            obj.Format = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.VoiceProfileId = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.Title = (reader.IsDBNull(9) ? String.Empty : reader.GetString(9));
            obj.ScriptMarkdown = (reader.IsDBNull(10) ? null : reader.GetString(10));
            obj.ScriptFromModel = reader.GetBoolean(11);
            obj.ArtifactId = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.ChaptersArtifactId = (reader.IsDBNull(13) ? null : reader.GetString(13));
            obj.ContentType = (reader.IsDBNull(14) ? null : reader.GetString(14));
            obj.Bytes = reader.GetInt64(15);
            obj.DurationSeconds = reader.GetDouble(16);
            obj.SegmentCount = reader.GetInt32(17);
            obj.Attempts = reader.GetInt32(18);
            obj.LockedUntilUtc = (reader.IsDBNull(19) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc));
            obj.ErrorMessage = (reader.IsDBNull(20) ? null : reader.GetString(20));
            obj.CompletedUtc = (reader.IsDBNull(21) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(21), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(22);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(23), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(24), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(MediaAssetFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.BriefingId))
            {
                args.Add(filter.BriefingId);
                clauses.Add("briefingid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.EditionId))
            {
                args.Add(filter.EditionId);
                clauses.Add("editionid = $" + args.Count);
            }
            if (filter.Kind.HasValue)
            {
                args.Add(filter.Kind.Value.ToString());
                clauses.Add("kind = $" + args.Count);
            }
            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
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
