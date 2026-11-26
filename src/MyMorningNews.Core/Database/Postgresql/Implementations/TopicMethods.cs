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
    /// PostgreSQL implementation of <see cref="ITopicMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class TopicMethods : PostgresqlMethodsBase, ITopicMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "name", "name" },
            { "status", "status" },
            { "lastRunUtc", "lastrunutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public TopicMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<bool> WithinSlotsAsync(Topic topic, long maximum, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(topic);
            // The owner's kept order comes first (lower slotpriority), then the oldest topics. The priority is read from the
            // row, not the caller's copy, so a selection saved after the topic was loaded still applies.
            List<bool> rows = await QueryAsync(@"SELECT count(*) < $5 FROM topics t, (SELECT coalesce(slotpriority, 2147483647) AS p FROM topics WHERE id=$4 AND tenantid=$1) me
WHERE t.tenantid=$1 AND t.userid=$2 AND NOT t.isonetime AND t.status <> 'Archived'
AND (coalesce(t.slotpriority, 2147483647), t.createdutc, t.id) < (me.p, $3, $4)", r => r.GetBoolean(0), token, topic.TenantId, topic.UserId, topic.CreatedUtc, topic.Id, maximum).ConfigureAwait(false);
            return rows.Count > 0 && rows[0] && topic.Status != TopicStatusEnum.Archived;
        }

        /// <inheritdoc />
        public async Task<int> SetSlotPrioritiesAsync(string tenantId, string userId, List<string> keepTopicIds, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            ArgumentNullException.ThrowIfNull(keepTopicIds);
            // One statement: every recurring topic of the owner gets its position in the list (1-based) or null.
            return await ExecuteAsync(@"UPDATE topics SET slotpriority = array_position($3::text[], id)
WHERE tenantid = $1 AND userid = $2 AND NOT isonetime AND slotpriority IS DISTINCT FROM array_position($3::text[], id)", token, tenantId, userId, keepTopicIds.ToArray()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Topic>> SlotOrderAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            return await QueryAsync("SELECT " + TopicQueries.SelectColumns + @" FROM topics WHERE tenantid = $1 AND userid = $2 AND NOT isonetime AND status <> 'Archived'
ORDER BY coalesce(slotpriority, 2147483647), createdutc, id", Map, token, tenantId, userId).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Topic> CreateAsync(Topic obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateTopicId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            long? maximum = Driver.TopicSlotLimit == null ? null : await Driver.TopicSlotLimit(obj.TenantId, obj.UserId, obj.IsOneTime, token).ConfigureAwait(false);
            if (obj.IsOneTime && maximum == 0) throw SlotExceeded(maximum);
            string insert = TopicQueries.Insert.Replace(" VALUES (", " SELECT ", StringComparison.Ordinal).TrimEnd(')');
            string sql = insert + @" WHERE $12 OR $11 = 'Archived' OR $39::bigint IS NULL OR
(SELECT count(*) FROM topics WHERE tenantid = $2 AND userid = $3 AND NOT isonetime AND status <> 'Archived') < $39 RETURNING " + TopicQueries.SelectColumns;
            List<Topic> rows = await Driver.QueryWithAdvisoryLockAsync(SlotLock(obj), sql, Map, new object?[]
            {
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Name,
                obj.Slug,
                obj.Description,
                obj.Brief,
                obj.AudienceLevel.ToString(),
                obj.Horizon.ToString(),
                obj.Language,
                obj.Status.ToString(),
                obj.IsOneTime,
                obj.NoveltyPreference,
                obj.EmailOnRun,
                obj.EmailWhenEmpty,
                obj.MaxEmailFindings,
                obj.DisplayThreshold,
                obj.WeightLlm,
                obj.WeightFeedback,
                obj.WeightNovelty,
                obj.WeightSource,
                obj.WeightRecency,
                obj.SeedQueries.ToArray(),
                obj.BriefingTemplateId,
                obj.LastRunUtc.HasValue ? DateTime.SpecifyKind(obj.LastRunUtc.Value, DateTimeKind.Utc) : null,
                obj.LastRefinedUtc.HasValue ? DateTime.SpecifyKind(obj.LastRefinedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.AgenticSearch,
                obj.McpToolIds.ToArray(),
                (object?)obj.VoiceProfileId,
                obj.AudioEnabled,
                obj.HistoryMode.ToString(),
                obj.HistoryCount,
                obj.HistorySinceUtc.HasValue ? DateTime.SpecifyKind(obj.HistorySinceUtc.Value, DateTimeKind.Utc) : null,
                obj.HistoryBriefingId,
                obj.HistoryTokenBudget, maximum
            }, token).ConfigureAwait(false);
            if (rows.Count == 0) throw SlotExceeded(maximum);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Topic?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Topic> rows = await QueryAsync(TopicQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Topic?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Topic> rows = await QueryAsync(TopicQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Topic> UpdateAsync(Topic obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            long? maximum = Driver.TopicSlotLimit == null ? null : await Driver.TopicSlotLimit(obj.TenantId, obj.UserId, obj.IsOneTime, token).ConfigureAwait(false);
            string sql = TopicQueries.Update + @" AND ($12 OR $11 = 'Archived' OR $38::bigint IS NULL OR
(NOT isonetime AND status <> 'Archived' AND userid = $3) OR
(SELECT count(*) FROM topics WHERE tenantid = $2 AND userid = $3 AND NOT isonetime AND status <> 'Archived' AND id <> $1) < $38)
RETURNING " + TopicQueries.SelectColumns;
            List<Topic> rows = await Driver.QueryWithAdvisoryLockAsync(SlotLock(obj), sql, Map, new object?[]
            {
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Name,
                obj.Slug,
                obj.Description,
                obj.Brief,
                obj.AudienceLevel.ToString(),
                obj.Horizon.ToString(),
                obj.Language,
                obj.Status.ToString(),
                obj.IsOneTime,
                obj.NoveltyPreference,
                obj.EmailOnRun,
                obj.EmailWhenEmpty,
                obj.MaxEmailFindings,
                obj.DisplayThreshold,
                obj.WeightLlm,
                obj.WeightFeedback,
                obj.WeightNovelty,
                obj.WeightSource,
                obj.WeightRecency,
                obj.SeedQueries.ToArray(),
                obj.BriefingTemplateId,
                obj.LastRunUtc.HasValue ? DateTime.SpecifyKind(obj.LastRunUtc.Value, DateTimeKind.Utc) : null,
                obj.LastRefinedUtc.HasValue ? DateTime.SpecifyKind(obj.LastRefinedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.AgenticSearch,
                obj.McpToolIds.ToArray(),
                (object?)obj.VoiceProfileId,
                obj.AudioEnabled,
                obj.HistoryMode.ToString(),
                obj.HistoryCount,
                obj.HistorySinceUtc.HasValue ? DateTime.SpecifyKind(obj.HistorySinceUtc.Value, DateTimeKind.Utc) : null,
                obj.HistoryBriefingId,
                obj.HistoryTokenBudget, maximum
            }, token).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                if (!await ExistsAsync(obj.TenantId, obj.Id, token).ConfigureAwait(false)) throw new KeyNotFoundException("Topic " + obj.Id + " was not found.");
                throw SlotExceeded(maximum);
            }
            return obj;
        }

        private static long SlotLock(Topic topic)
        {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("topics:" + topic.TenantId + ":" + topic.UserId));
            return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
        }

        private static MyMorningNews.Core.Exceptions.CapReachedException SlotExceeded(long? maximum)
        {
            return new MyMorningNews.Core.Exceptions.CapReachedException("topics.max", maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unlimited", null,
                "Your plan has no available custom topic slots. Archive a topic or change your plan to continue.");
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(TopicQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(TopicQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(TopicQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Topic>> EnumerateAsync(TopicFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + TopicQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + TopicQueries.SelectColumns + " FROM " + TopicQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Topic> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Topic>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(TopicFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + TopicQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> DeleteCascadeAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(topicId)) throw new ArgumentNullException(nameof(topicId));
            string[] statements =
            {
                "DELETE FROM schedules WHERE tenantid = $1 AND ownertype = 'Topic' AND ownerid = $2",
                "DELETE FROM editiontopics WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM briefingitems WHERE tenantid = $1 AND briefingid IN (SELECT id FROM briefings WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM briefingsections WHERE tenantid = $1 AND briefingid IN (SELECT id FROM briefings WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM artifacts WHERE tenantid = $1 AND (topicid = $2 OR briefingid IN (SELECT id FROM briefings WHERE tenantid = $1 AND topicid = $2))",
                "DELETE FROM mediachapters WHERE tenantid = $1 AND mediaassetid IN (SELECT id FROM mediaassets WHERE tenantid = $1 AND briefingid IN (SELECT id FROM briefings WHERE tenantid = $1 AND topicid = $2))",
                "DELETE FROM mediaassets WHERE tenantid = $1 AND briefingid IN (SELECT id FROM briefings WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM briefings WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM findingevidence WHERE tenantid = $1 AND findingid IN (SELECT id FROM findings WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM evidence WHERE tenantid = $1 AND runid IN (SELECT id FROM runs WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM runstages WHERE tenantid = $1 AND runid IN (SELECT id FROM runs WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM runtoolcalls WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM searchresults WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM searchqueries WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM embeddings WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM findings WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM predictions WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM suggestions WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM runs WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM researchrequests WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM emailactiontokens WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM emailsuppressions WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM feedbackreasontags WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM learnedpreferencefeedback WHERE tenantid = $1 AND learnedpreferenceid IN (SELECT id FROM learnedpreferences WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM learnedpreferencefeedback WHERE tenantid = $1 AND feedbackid IN (SELECT id FROM feedback WHERE tenantid = $1 AND topicid = $2)",
                "DELETE FROM learnedpreferences WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM feedback WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM learningcheckpoints WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM modelassignments WHERE tenantid = $1 AND topicid = $2",
                "DELETE FROM prompttemplates WHERE tenantid = $1 AND topicid = $2",
                "UPDATE emailmessages SET topicid = NULL WHERE tenantid = $1 AND topicid = $2"
            };
            foreach (string sql in statements) await ExecuteAsync(sql, token, tenantId, topicId).ConfigureAwait(false);
            return await ExecuteAsync("DELETE FROM topics WHERE tenantid = $1 AND id = $2", token, tenantId, topicId).ConfigureAwait(false) > 0;
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="TopicQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Topic Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Topic obj = new Topic();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Name = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Slug = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.Description = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Brief = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.AudienceLevel = Enum.Parse<AudienceLevelEnum>(reader.GetString(7));
            obj.Horizon = Enum.Parse<HorizonEnum>(reader.GetString(8));
            obj.Language = (reader.IsDBNull(9) ? String.Empty : reader.GetString(9));
            obj.Status = Enum.Parse<TopicStatusEnum>(reader.GetString(10));
            obj.IsOneTime = reader.GetBoolean(11);
            obj.NoveltyPreference = reader.GetInt32(12);
            obj.EmailOnRun = reader.GetBoolean(13);
            obj.EmailWhenEmpty = reader.GetBoolean(14);
            obj.MaxEmailFindings = reader.GetInt32(15);
            obj.DisplayThreshold = (reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16));
            obj.WeightLlm = (reader.IsDBNull(17) ? (double?)null : reader.GetDouble(17));
            obj.WeightFeedback = (reader.IsDBNull(18) ? (double?)null : reader.GetDouble(18));
            obj.WeightNovelty = (reader.IsDBNull(19) ? (double?)null : reader.GetDouble(19));
            obj.WeightSource = (reader.IsDBNull(20) ? (double?)null : reader.GetDouble(20));
            obj.WeightRecency = (reader.IsDBNull(21) ? (double?)null : reader.GetDouble(21));
            obj.SeedQueries = (reader.IsDBNull(22) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(22)));
            obj.BriefingTemplateId = (reader.IsDBNull(23) ? null : reader.GetString(23));
            obj.LastRunUtc = (reader.IsDBNull(24) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(24), DateTimeKind.Utc));
            obj.LastRefinedUtc = (reader.IsDBNull(25) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(25), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(26);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(27), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(28), DateTimeKind.Utc);
            obj.AgenticSearch = reader.GetBoolean(29);
            obj.McpToolIds = reader.IsDBNull(30) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(30));
            obj.VoiceProfileId = (reader.IsDBNull(31) ? null : reader.GetString(31));
            obj.AudioEnabled = reader.GetBoolean(32);
            obj.HistoryMode = Enum.Parse<HistoryModeEnum>(reader.GetString(33));
            obj.HistoryCount = reader.GetInt32(34);
            obj.HistorySinceUtc = reader.IsDBNull(35) ? null : DateTime.SpecifyKind(reader.GetDateTime(35), DateTimeKind.Utc);
            obj.HistoryBriefingId = reader.IsDBNull(36) ? null : reader.GetString(36);
            obj.HistoryTokenBudget = reader.GetInt32(37);
            obj.SlotPriority = reader.FieldCount > 38 && !reader.IsDBNull(38) ? reader.GetInt32(38) : null;
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(TopicFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.Slug))
            {
                args.Add(filter.Slug);
                clauses.Add("slug = $" + args.Count);
            }
            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
            }
            if (filter.IsOneTime.HasValue)
            {
                args.Add(filter.IsOneTime.Value);
                clauses.Add("isonetime = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(name ILIKE $" + args.Count + " OR description ILIKE $" + args.Count + ")");
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
