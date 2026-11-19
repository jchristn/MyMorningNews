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
    /// PostgreSQL implementation of <see cref="IFeedbackMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class FeedbackMethods : PostgresqlMethodsBase, IFeedbackMethods
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
        public FeedbackMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Feedback> CreateAsync(Feedback obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateFeedbackId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(FeedbackQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.TargetType.ToString(),
                obj.TargetId,
                obj.TopicId,
                (object?)obj.Relevance?.ToString(),
                (object?)obj.Accuracy?.ToString(),
                (object?)obj.Usefulness?.ToString(),
                obj.Reason,
                obj.Channel.ToString(),
                obj.EmailMessageId,
                obj.SupersedesFeedbackId,
                obj.IsCurrent,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Feedback?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Feedback> rows = await QueryAsync(FeedbackQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Feedback?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Feedback> rows = await QueryAsync(FeedbackQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Feedback> UpdateAsync(Feedback obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(FeedbackQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.TargetType.ToString(),
                obj.TargetId,
                obj.TopicId,
                (object?)obj.Relevance?.ToString(),
                (object?)obj.Accuracy?.ToString(),
                (object?)obj.Usefulness?.ToString(),
                obj.Reason,
                obj.Channel.ToString(),
                obj.EmailMessageId,
                obj.SupersedesFeedbackId,
                obj.IsCurrent,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Feedback " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(FeedbackQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(FeedbackQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(FeedbackQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Feedback>> EnumerateAsync(FeedbackFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + FeedbackQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + FeedbackQueries.Columns + " FROM " + FeedbackQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Feedback> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Feedback>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(FeedbackFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + FeedbackQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<string?> SupersedeAsync(string tenantId, string userId, string targetType, string targetId, CancellationToken token = default)
        {
            List<string> rows = await QueryAsync("UPDATE feedback SET iscurrent = FALSE, lastupdateutc = $5 WHERE tenantid = $1 AND userid = $2 AND targettype = $3 AND targetid = $4 AND iscurrent = TRUE RETURNING id",
                r => r.GetString(0), token, tenantId, userId, targetType, targetId, DateTime.UtcNow).ConfigureAwait(false);
            return rows.OrderByDescending(id => id, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<LearningCandidate>> LearningCandidatesAsync(int minimum, int limit, CancellationToken token = default)
        {
            string sql = "SELECT f.tenantid, f.userid, f.topicid, COUNT(*), MAX(f.createdutc) FROM feedback f " +
                "LEFT JOIN learningcheckpoints c ON c.tenantid = f.tenantid AND c.userid = f.userid AND c.topicid = f.topicid " +
                "WHERE f.topicid IS NOT NULL AND (c.lastfeedbackutc IS NULL OR f.createdutc > c.lastfeedbackutc) " +
                "GROUP BY f.tenantid, f.userid, f.topicid HAVING COUNT(*) >= $1 ORDER BY MAX(f.createdutc) LIMIT $2";
            return await QueryAsync(sql, r => new LearningCandidate
            {
                TenantId = r.GetString(0),
                UserId = r.GetString(1),
                TopicId = r.GetString(2),
                NewFeedback = (int)r.GetInt64(3),
                NewestUtc = DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc)
            }, token, Math.Max(1, minimum), Math.Clamp(limit, 1, 500)).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="FeedbackQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Feedback Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Feedback obj = new Feedback();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.TargetType = Enum.Parse<FeedbackTargetTypeEnum>(reader.GetString(3));
            obj.TargetId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.TopicId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Relevance = (reader.IsDBNull(6) ? null : Enum.Parse<RelevanceRatingEnum>(reader.GetString(6)));
            obj.Accuracy = (reader.IsDBNull(7) ? null : Enum.Parse<AccuracyRatingEnum>(reader.GetString(7)));
            obj.Usefulness = (reader.IsDBNull(8) ? null : Enum.Parse<UsefulnessRatingEnum>(reader.GetString(8)));
            obj.Reason = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.Channel = Enum.Parse<FeedbackChannelEnum>(reader.GetString(10));
            obj.EmailMessageId = (reader.IsDBNull(11) ? null : reader.GetString(11));
            obj.SupersedesFeedbackId = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.IsCurrent = reader.GetBoolean(13);
            obj.Active = reader.GetBoolean(14);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(FeedbackFilter filter, List<object?> args)
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
            if (filter.TargetType.HasValue)
            {
                args.Add(filter.TargetType.Value.ToString());
                clauses.Add("targettype = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TargetId))
            {
                args.Add(filter.TargetId);
                clauses.Add("targetid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }
            if (filter.Relevance.HasValue)
            {
                args.Add(filter.Relevance.Value.ToString());
                clauses.Add("relevance = $" + args.Count);
            }
            if (filter.Accuracy.HasValue)
            {
                args.Add(filter.Accuracy.Value.ToString());
                clauses.Add("accuracy = $" + args.Count);
            }
            if (filter.Usefulness.HasValue)
            {
                args.Add(filter.Usefulness.Value.ToString());
                clauses.Add("usefulness = $" + args.Count);
            }
            if (filter.Channel.HasValue)
            {
                args.Add(filter.Channel.Value.ToString());
                clauses.Add("channel = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.EmailMessageId))
            {
                args.Add(filter.EmailMessageId);
                clauses.Add("emailmessageid = $" + args.Count);
            }
            if (filter.IsCurrent.HasValue)
            {
                args.Add(filter.IsCurrent.Value);
                clauses.Add("iscurrent = $" + args.Count);
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
