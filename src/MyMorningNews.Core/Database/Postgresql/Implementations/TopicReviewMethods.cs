namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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
    /// PostgreSQL implementation of <see cref="ITopicReviewMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class TopicReviewMethods : PostgresqlMethodsBase, ITopicReviewMethods
    {
        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "createdUtc", "createdutc" },
            { "deleteAfterUtc", "deleteafterutc" },
            { "promptedUtc", "promptedutc" }
        };

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public TopicReviewMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        /// <inheritdoc />
        public async Task<TopicReview> CreateAsync(TopicReview obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateTopicReviewId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(TopicReviewQueries.Insert, token,
                obj.Id, obj.TenantId, obj.UserId, obj.TopicId, obj.Status.ToString(), Utc(obj.LastNewFindingUtc),
                DateTime.SpecifyKind(obj.PromptedUtc, DateTimeKind.Utc), DateTime.SpecifyKind(obj.DeleteAfterUtc, DateTimeKind.Utc),
                Utc(obj.ReminderSentUtc), Utc(obj.ResolvedUtc), obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc), DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<TopicReview?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<TopicReview> rows = await QueryAsync(TopicReviewQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<TopicReview> UpdateAsync(TopicReview obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(TopicReviewQueries.Update, token,
                obj.Id, obj.TenantId, obj.Status.ToString(), Utc(obj.LastNewFindingUtc), DateTime.SpecifyKind(obj.DeleteAfterUtc, DateTimeKind.Utc),
                Utc(obj.ReminderSentUtc), Utc(obj.ResolvedUtc), obj.Active, DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("TopicReview " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<TopicReview>> EnumerateAsync(TopicReviewFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + TopicReviewQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + TopicReviewQueries.Columns + " FROM " + TopicReviewQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<TopicReview> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<TopicReview>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<List<TopicReview>> ListPendingAsync(int limit, CancellationToken token = default)
        {
            return await QueryAsync(TopicReviewQueries.SelectPending, Map, token, Math.Clamp(limit, 1, 5000)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<StaleTopicCandidate>> ListStaleCandidatesAsync(DateTime windowStartUtc, DateTime snoozeCutoffUtc, int limit, CancellationToken token = default)
        {
            return await QueryAsync(TopicReviewQueries.SelectStaleCandidates, r => new StaleTopicCandidate
            {
                TopicId = r.GetString(0),
                TenantId = r.GetString(1),
                UserId = r.GetString(2),
                LastNewFindingUtc = r.IsDBNull(3) ? null : DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc)
            }, token, DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc), DateTime.SpecifyKind(snoozeCutoffUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 5000)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<DateTime?> ReadLastNewFindingAsync(string tenantId, string topicId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(topicId)) throw new ArgumentNullException(nameof(topicId));
            List<DateTime?> rows = await QueryAsync(TopicReviewQueries.SelectLastNewFinding, r => r.IsDBNull(0) ? (DateTime?)null : DateTime.SpecifyKind(r.GetDateTime(0), DateTimeKind.Utc), token, tenantId, topicId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="TopicReviewQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        /// <exception cref="ArgumentNullException">reader is null.</exception>
        public static TopicReview Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            TopicReview obj = new TopicReview();
            obj.Id = reader.GetString(0);
            obj.TenantId = reader.GetString(1);
            obj.UserId = reader.GetString(2);
            obj.TopicId = reader.GetString(3);
            obj.Status = Enum.TryParse(reader.GetString(4), out TopicReviewStatusEnum status) ? status : TopicReviewStatusEnum.Pending;
            obj.LastNewFindingUtc = reader.IsDBNull(5) ? null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc);
            obj.PromptedUtc = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc);
            obj.DeleteAfterUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);
            obj.ReminderSentUtc = reader.IsDBNull(8) ? null : DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc);
            obj.ResolvedUtc = reader.IsDBNull(9) ? null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            obj.Active = reader.GetBoolean(10);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc);
            return obj;
        }

        private static object? Utc(DateTime? value)
        {
            return value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
        }

        private static string BuildWhere(TopicReviewFilter filter, List<object?> args)
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

            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }

            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
            }

            return " WHERE " + String.Join(" AND ", clauses);
        }
    }
}
