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
    /// PostgreSQL implementation of <see cref="IBriefingMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class BriefingMethods : PostgresqlMethodsBase, IBriefingMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "composedUtc", "composedutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public BriefingMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Briefing> CreateAsync(Briefing obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateBriefingId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(BriefingQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Kind.ToString(),
                obj.EditionId,
                obj.TopicId,
                obj.RunId,
                obj.ResearchRequestId,
                obj.ScheduleId,
                obj.PeriodStartUtc.HasValue ? DateTime.SpecifyKind(obj.PeriodStartUtc.Value, DateTimeKind.Utc) : null,
                obj.PeriodEndUtc.HasValue ? DateTime.SpecifyKind(obj.PeriodEndUtc.Value, DateTimeKind.Utc) : null,
                obj.Title,
                obj.Markdown,
                obj.Status.ToString(),
                obj.ComposeAfterUtc.HasValue ? DateTime.SpecifyKind(obj.ComposeAfterUtc.Value, DateTimeKind.Utc) : null,
                obj.ResearchDeadlineUtc.HasValue ? DateTime.SpecifyKind(obj.ResearchDeadlineUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.BriefingTemplateVersionId,
                obj.MediaAssetId,
                obj.MarkdownArtifactId,
                obj.HtmlArtifactId,
                obj.ItemCount,
                obj.WordCount,
                obj.ReadMinutes,
                obj.ModelFallbacks,
                obj.ErrorMessage,
                obj.Pinned,
                obj.IsDryRun,
                obj.ComposedUtc.HasValue ? DateTime.SpecifyKind(obj.ComposedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Briefing?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Briefing> rows = await QueryAsync(BriefingQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Briefing?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Briefing> rows = await QueryAsync(BriefingQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Briefing> UpdateAsync(Briefing obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(BriefingQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Kind.ToString(),
                obj.EditionId,
                obj.TopicId,
                obj.RunId,
                obj.ResearchRequestId,
                obj.ScheduleId,
                obj.PeriodStartUtc.HasValue ? DateTime.SpecifyKind(obj.PeriodStartUtc.Value, DateTimeKind.Utc) : null,
                obj.PeriodEndUtc.HasValue ? DateTime.SpecifyKind(obj.PeriodEndUtc.Value, DateTimeKind.Utc) : null,
                obj.Title,
                obj.Markdown,
                obj.Status.ToString(),
                obj.ComposeAfterUtc.HasValue ? DateTime.SpecifyKind(obj.ComposeAfterUtc.Value, DateTimeKind.Utc) : null,
                obj.ResearchDeadlineUtc.HasValue ? DateTime.SpecifyKind(obj.ResearchDeadlineUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.BriefingTemplateVersionId,
                obj.MediaAssetId,
                obj.MarkdownArtifactId,
                obj.HtmlArtifactId,
                obj.ItemCount,
                obj.WordCount,
                obj.ReadMinutes,
                obj.ModelFallbacks,
                obj.ErrorMessage,
                obj.Pinned,
                obj.IsDryRun,
                obj.ComposedUtc.HasValue ? DateTime.SpecifyKind(obj.ComposedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Briefing " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BriefingQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(BriefingQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BriefingQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Briefing>> EnumerateAsync(BriefingFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BriefingQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + BriefingQueries.Columns + " FROM " + BriefingQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Briefing> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Briefing>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(BriefingFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BriefingQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<Briefing>> ClaimQueuedAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, IReadOnlyCollection<string>? tenantIds = null, CancellationToken token = default)
        {
            bool scoped = tenantIds != null;
            string sql = "UPDATE briefings SET status = 'Composing', lockeduntilutc = $2, lastupdateutc = $1 WHERE id IN (SELECT id FROM briefings WHERE status = 'Queued' " +
                "AND (composeafterutc IS NULL OR composeafterutc <= $1) " + (scoped ? "AND tenantid = ANY($4) " : String.Empty) +
                "ORDER BY composeafterutc NULLS FIRST, createdutc FOR UPDATE SKIP LOCKED LIMIT $3) RETURNING " + BriefingQueries.Columns;
            List<object?> args = new List<object?> { DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), DateTime.SpecifyKind(lockedUntilUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 50) };
            if (scoped) args.Add(tenantIds!.ToArray());
            return await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> FailAbandonedAsync(DateTime nowUtc, CancellationToken token = default)
        {
            return await ExecuteAsync("UPDATE briefings SET status = 'Failed', errormessage = 'Interrupted: the worker stopped before the briefing was composed.', lastupdateutc = $1 " +
                "WHERE status = 'Composing' AND lockeduntilutc IS NOT NULL AND lockeduntilutc < $1", token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> HasActiveForEditionAsync(string tenantId, string editionId, CancellationToken token = default)
        {
            List<long> rows = await QueryAsync("SELECT COUNT(*) FROM briefings WHERE tenantid = $1 AND editionid = $2 AND status IN ('Queued', 'Composing')", r => r.GetInt64(0), token, tenantId, editionId).ConfigureAwait(false);
            return rows.FirstOrDefault() > 0;
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="BriefingQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Briefing Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Briefing obj = new Briefing();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Kind = Enum.Parse<BriefingKindEnum>(reader.GetString(3));
            obj.EditionId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.TopicId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.RunId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.ResearchRequestId = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.ScheduleId = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.PeriodStartUtc = (reader.IsDBNull(9) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
            obj.PeriodEndUtc = (reader.IsDBNull(10) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc));
            obj.Title = (reader.IsDBNull(11) ? String.Empty : reader.GetString(11));
            obj.Markdown = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.Status = Enum.Parse<BriefingStatusEnum>(reader.GetString(13));
            obj.ComposeAfterUtc = (reader.IsDBNull(14) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc));
            obj.ResearchDeadlineUtc = (reader.IsDBNull(15) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc));
            obj.LockedUntilUtc = (reader.IsDBNull(16) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc));
            obj.BriefingTemplateVersionId = (reader.IsDBNull(17) ? null : reader.GetString(17));
            obj.MediaAssetId = (reader.IsDBNull(18) ? null : reader.GetString(18));
            obj.MarkdownArtifactId = (reader.IsDBNull(19) ? null : reader.GetString(19));
            obj.HtmlArtifactId = (reader.IsDBNull(20) ? null : reader.GetString(20));
            obj.ItemCount = reader.GetInt32(21);
            obj.WordCount = reader.GetInt32(22);
            obj.ReadMinutes = reader.GetInt32(23);
            obj.ModelFallbacks = reader.GetInt32(24);
            obj.ErrorMessage = (reader.IsDBNull(25) ? null : reader.GetString(25));
            obj.Pinned = reader.GetBoolean(26);
            obj.IsDryRun = reader.GetBoolean(27);
            obj.ComposedUtc = (reader.IsDBNull(28) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(28), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(29);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(30), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(31), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(BriefingFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.EditionId))
            {
                args.Add(filter.EditionId);
                clauses.Add("editionid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.RunId))
            {
                args.Add(filter.RunId);
                clauses.Add("runid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.ResearchRequestId))
            {
                args.Add(filter.ResearchRequestId);
                clauses.Add("researchrequestid = $" + args.Count);
            }
            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
            }
            if (filter.Pinned.HasValue)
            {
                args.Add(filter.Pinned.Value);
                clauses.Add("pinned = $" + args.Count);
            }
            if (filter.IsDryRun.HasValue)
            {
                args.Add(filter.IsDryRun.Value);
                clauses.Add("isdryrun = $" + args.Count);
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
