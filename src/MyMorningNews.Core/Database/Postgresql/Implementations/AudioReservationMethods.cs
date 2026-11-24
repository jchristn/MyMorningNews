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
    /// PostgreSQL implementation of <see cref="IAudioReservationMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class AudioReservationMethods : PostgresqlMethodsBase, IAudioReservationMethods
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
        public AudioReservationMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<AudioUsageSummary> UsageAsync(string tenantId, string userId, DateTime periodStartUtc, CancellationToken token = default)
        {
            const string sql = @"SELECT
COALESCE((SELECT SUM(milliseconds) FROM audiousageentries WHERE tenantid = $1 AND userid = $2 AND periodstartutc = $3), 0)::bigint,
COALESCE((SELECT SUM(reservedseconds) FROM audioreservations WHERE tenantid = $1 AND userid = $2 AND periodstartutc = $3 AND status = 'Reserved'), 0)::bigint";
            return (await QueryAsync(sql, r => new AudioUsageSummary { GeneratedMilliseconds = r.GetInt64(0), ReservedSeconds = r.GetInt64(1) }, token,
                tenantId, userId, periodStartUtc).ConfigureAwait(false))[0];
        }

        /// <inheritdoc />
        public async Task RecoverAsync(DateTime nowUtc, CancellationToken token = default)
        {
            // Reservations do not expire just because a slow live job exceeded its initial estimate.
            await ExecuteAsync(@"UPDATE audioreservations r SET status = 'Released', reservedseconds = 0, lastupdateutc = $1
WHERE r.status = 'Reserved' AND r.expiresutc < $1 AND NOT EXISTS
(SELECT 1 FROM mediaassets m WHERE m.id = r.operationid AND m.tenantid = r.tenantid AND m.status NOT IN ('Failed'))", token, nowUtc).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AudioReservation?> TryReserveAsync(AudioReservation reservation, long? maximumSeconds, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(reservation);
            if (reservation.ReservedSeconds < 1 || maximumSeconds < 0) throw new ArgumentOutOfRangeException(nameof(reservation));
            string sql = @"WITH existing AS (
 SELECT * FROM audioreservations WHERE tenantid = $2 AND userid = $3 AND operationid = $4),
held AS (SELECT COALESCE(SUM(CASE WHEN status = 'Committed' THEN committedmilliseconds WHEN status = 'Reserved' THEN reservedseconds * 1000 ELSE 0 END), 0) AS used
 FROM audioreservations WHERE tenantid = $2 AND userid = $3 AND periodstartutc = $5 AND operationid <> $4),
reserved AS (
 INSERT INTO audioreservations (" + AudioReservationQueries.Columns + @")
 SELECT $1, $2, $3, $4, $5, $6, 0, 'Reserved', $7, TRUE, now(), now()
 WHERE NOT EXISTS (SELECT 1 FROM existing) AND ($8::bigint IS NULL OR (SELECT used FROM held) + $6 * 1000 <= $8 * 1000)
 RETURNING " + AudioReservationQueries.Columns + @")
SELECT " + AudioReservationQueries.Columns + @" FROM reserved UNION ALL
SELECT " + AudioReservationQueries.Columns + " FROM existing WHERE status <> 'Released'";
            List<AudioReservation> rows = await Driver.QueryWithAdvisoryLockAsync(AccountLock(reservation.TenantId, reservation.UserId), sql, Map,
                new object?[] { reservation.Id, reservation.TenantId, reservation.UserId, reservation.OperationId,
                    reservation.PeriodStartUtc, reservation.ReservedSeconds, reservation.ExpiresUtc, maximumSeconds }, token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<MediaAsset?> TryQueueAsync(MediaAsset asset, AudioReservation reservation, long? maximumSeconds, bool force, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            ArgumentNullException.ThrowIfNull(reservation);
            if (asset.Id != reservation.OperationId || asset.TenantId != reservation.TenantId || asset.UserId != reservation.UserId || reservation.ReservedSeconds < 1) throw new ArgumentException("Reservation must belong to the queued episode.");
            string sql = @"WITH existing AS (SELECT * FROM mediaassets WHERE tenantid = $2 AND userid = $3 AND briefingid = $4
 AND status <> 'Failed' ORDER BY createdutc DESC LIMIT 1),
reuse AS (SELECT * FROM existing WHERE NOT $14::boolean OR status <> 'Ready'),
held AS (SELECT COALESCE(SUM(CASE WHEN status = 'Committed' THEN committedmilliseconds WHEN status = 'Reserved' THEN reservedseconds * 1000 ELSE 0 END), 0) AS used
 FROM audioreservations WHERE tenantid = $2 AND userid = $3 AND periodstartutc = $11),
queued AS (INSERT INTO mediaassets (" + MediaAssetQueries.Columns + @")
 SELECT $1,$2,$3,$4,$5,$6,'Queued',$7,$8,$9,NULL,FALSE,NULL,NULL,NULL,0,0,0,0,NULL,NULL,NULL,TRUE,now(),now()
 WHERE NOT EXISTS (SELECT 1 FROM reuse) AND ($15::bigint IS NULL OR (SELECT used FROM held) + $12 * 1000 <= $15 * 1000)
 RETURNING " + MediaAssetQueries.Columns + @"),
reserved AS (INSERT INTO audioreservations (" + AudioReservationQueries.Columns + @")
 SELECT $10,$2,$3,$1,$11,$12,0,'Reserved',$13,TRUE,now(),now() FROM queued RETURNING id)
SELECT " + MediaAssetQueries.Columns + @" FROM queued WHERE EXISTS (SELECT 1 FROM reserved) UNION ALL
SELECT " + MediaAssetQueries.Columns + " FROM reuse";
            List<MediaAsset> rows = await Driver.QueryWithAdvisoryLockAsync(AccountLock(asset.TenantId, asset.UserId), sql, MediaAssetMethods.Map,
                new object?[] { asset.Id, asset.TenantId, asset.UserId, asset.BriefingId, asset.EditionId, asset.Kind.ToString(), asset.Format,
                    asset.VoiceProfileId, asset.Title, reservation.Id, reservation.PeriodStartUtc, reservation.ReservedSeconds, reservation.ExpiresUtc, force, maximumSeconds }, token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AudioReservation?> TryCommitSampleAsync(string tenantId, string userId, string operationId, long milliseconds, long? maximumSeconds, CancellationToken token = default)
        {
            if (milliseconds < 1 || maximumSeconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            string sql = @"WITH own AS (SELECT * FROM audioreservations WHERE tenantid = $1 AND userid = $2 AND operationid = $3 AND status <> 'Released'
 AND NOT EXISTS (SELECT 1 FROM mediaassets WHERE id = $3)),
held AS (SELECT COALESCE(SUM(CASE WHEN status = 'Committed' THEN committedmilliseconds WHEN status = 'Reserved' THEN reservedseconds * 1000 ELSE 0 END), 0) AS used
 FROM audioreservations WHERE tenantid = $1 AND userid = $2 AND periodstartutc = (SELECT periodstartutc FROM own) AND operationid <> $3),
settled AS (UPDATE audioreservations SET status = 'Committed', committedmilliseconds = $4, reservedseconds = 0, lastupdateutc = now()
 WHERE id IN (SELECT id FROM own WHERE status = 'Reserved') AND ($5::bigint IS NULL OR (SELECT used FROM held) + $4 <= $5 * 1000)
 RETURNING " + AudioReservationQueries.Columns + @"),
ledger AS (INSERT INTO audiousageentries (id,tenantid,userid,reservationid,operationid,periodstartutc,milliseconds)
 SELECT $6,tenantid,userid,id,operationid,periodstartutc,committedmilliseconds FROM settled ON CONFLICT (reservationid) DO NOTHING)
SELECT " + AudioReservationQueries.Columns + @" FROM settled UNION ALL
SELECT " + AudioReservationQueries.Columns + " FROM own WHERE status = 'Committed' AND committedmilliseconds = $4";
            return (await Driver.QueryWithAdvisoryLockAsync(AccountLock(tenantId, userId), sql, Map,
                new object?[] { tenantId, userId, operationId, milliseconds, maximumSeconds, IdGenerator.GenerateAudioUsageEntryId() }, token).ConfigureAwait(false)).FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AudioReservation?> TryCommitAsync(MediaAsset asset, long milliseconds, long? maximumSeconds, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (milliseconds < 1 || maximumSeconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            string sql = @"WITH own AS (
 SELECT * FROM audioreservations WHERE tenantid = $1 AND userid = $2 AND operationid = $3 AND status <> 'Released'),
held AS (SELECT COALESCE(SUM(CASE WHEN status = 'Committed' THEN committedmilliseconds WHEN status = 'Reserved' THEN reservedseconds * 1000 ELSE 0 END), 0) AS used
 FROM audioreservations WHERE tenantid = $1 AND userid = $2 AND periodstartutc = (SELECT periodstartutc FROM own) AND operationid <> $3),
settled AS (
 UPDATE audioreservations SET status = 'Committed', committedmilliseconds = $4, reservedseconds = 0, lastupdateutc = now()
 WHERE id IN (SELECT id FROM own WHERE status = 'Reserved')
 AND EXISTS (SELECT 1 FROM mediaassets WHERE id = $3 AND tenantid = $1 AND userid = $2)
 AND ($5::bigint IS NULL OR (SELECT used FROM held) + $4 <= $5 * 1000)
 RETURNING " + AudioReservationQueries.Columns + @"),
ledger AS (INSERT INTO audiousageentries (id, tenantid, userid, reservationid, operationid, periodstartutc, milliseconds)
 SELECT $12, tenantid, userid, id, operationid, periodstartutc, committedmilliseconds FROM settled ON CONFLICT (reservationid) DO NOTHING),
valid AS (SELECT * FROM settled UNION ALL SELECT * FROM own WHERE status = 'Committed' AND committedmilliseconds = $4),
published AS (
 UPDATE mediaassets SET artifactid = $6, chaptersartifactid = $7, contenttype = $8, bytes = $9, durationseconds = $10,
 segmentcount = $11, status = 'Ready', errormessage = NULL, lockeduntilutc = NULL, completedutc = now(), lastupdateutc = now()
 WHERE id = $3 AND tenantid = $1 AND userid = $2 AND EXISTS (SELECT 1 FROM valid) RETURNING id, briefingid),
linked AS (UPDATE briefings SET mediaassetid = $3, lastupdateutc = now() WHERE id IN (SELECT briefingid FROM published) AND tenantid = $1 RETURNING id)
SELECT " + AudioReservationQueries.Columns + " FROM valid WHERE EXISTS (SELECT 1 FROM published)";
            List<AudioReservation> rows = await Driver.QueryWithAdvisoryLockAsync(AccountLock(asset.TenantId, asset.UserId), sql, Map,
                new object?[] { asset.TenantId, asset.UserId, asset.Id, milliseconds, maximumSeconds, asset.ArtifactId,
                    asset.ChaptersArtifactId, asset.ContentType, asset.Bytes, asset.DurationSeconds, asset.SegmentCount, IdGenerator.GenerateAudioUsageEntryId() }, token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(string tenantId, string userId, string operationId, CancellationToken token = default)
        {
            await Driver.QueryWithAdvisoryLockAsync(AccountLock(tenantId, userId),
                "UPDATE audioreservations SET status = 'Released', reservedseconds = 0, lastupdateutc = now() WHERE tenantid = $1 AND userid = $2 AND operationid = $3 AND status = 'Reserved' RETURNING id",
                r => r.GetString(0), new object?[] { tenantId, userId, operationId }, token).ConfigureAwait(false);
        }

        private static long AccountLock(string tenantId, string userId)
        {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("audio:" + tenantId + ":" + userId));
            return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
        }

        /// <inheritdoc />
        public async Task<AudioReservation> CreateAsync(AudioReservation obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateAudioReservationId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(AudioReservationQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.OperationId,
                DateTime.SpecifyKind(obj.PeriodStartUtc, DateTimeKind.Utc),
                obj.ReservedSeconds,
                obj.CommittedMilliseconds,
                obj.Status,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<AudioReservation?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<AudioReservation> rows = await QueryAsync(AudioReservationQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AudioReservation?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<AudioReservation> rows = await QueryAsync(AudioReservationQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<AudioReservation> UpdateAsync(AudioReservation obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(AudioReservationQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.OperationId,
                DateTime.SpecifyKind(obj.PeriodStartUtc, DateTimeKind.Utc),
                obj.ReservedSeconds,
                obj.CommittedMilliseconds,
                obj.Status,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("AudioReservation " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(AudioReservationQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(AudioReservationQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(AudioReservationQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AudioReservation>> EnumerateAsync(AudioReservationFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + AudioReservationQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + AudioReservationQueries.Columns + " FROM " + AudioReservationQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<AudioReservation> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<AudioReservation>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(AudioReservationFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + AudioReservationQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="AudioReservationQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static AudioReservation Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            AudioReservation obj = new AudioReservation();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.OperationId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.PeriodStartUtc = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc);
            obj.ReservedSeconds = reader.GetInt64(5);
            obj.CommittedMilliseconds = reader.GetInt64(6);
            obj.Status = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.ExpiresUtc = DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc);
            obj.Active = reader.GetBoolean(9);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(AudioReservationFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.OperationId))
            {
                args.Add(filter.OperationId);
                clauses.Add("operationid = $" + args.Count);
            }
            if (filter.PeriodStartUtc.HasValue)
            {
                args.Add(filter.PeriodStartUtc.Value);
                clauses.Add("periodstartutc = $" + args.Count);
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
