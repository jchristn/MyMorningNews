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
    /// PostgreSQL implementation of <see cref="IResearchRequestMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class ResearchRequestMethods : PostgresqlMethodsBase, IResearchRequestMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "title", "title" },
            { "status", "status" },
            { "runAtUtc", "runatutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public ResearchRequestMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ResearchRequest> CreateAsync(ResearchRequest obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateResearchRequestId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(ResearchRequestQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.TopicId,
                obj.RequestText,
                obj.Title,
                obj.Deliverables.ToArray(),
                obj.Length.ToString(),
                obj.Status.ToString(),
                obj.RunAtUtc.HasValue ? DateTime.SpecifyKind(obj.RunAtUtc.Value, DateTimeKind.Utc) : null,
                obj.EmailRecipients.ToArray(),
                obj.PublishToFeed,
                obj.OutOfScopeReason,
                obj.EstimatedCostUsd,
                obj.RunId,
                obj.BriefingId,
                obj.ConvertedTopicId,
                obj.ErrorMessage,
                obj.ConfirmedUtc.HasValue ? DateTime.SpecifyKind(obj.ConfirmedUtc.Value, DateTimeKind.Utc) : null,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<ResearchRequest?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<ResearchRequest> rows = await QueryAsync(ResearchRequestQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<ResearchRequest?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<ResearchRequest> rows = await QueryAsync(ResearchRequestQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<ResearchRequest> UpdateAsync(ResearchRequest obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(ResearchRequestQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.TopicId,
                obj.RequestText,
                obj.Title,
                obj.Deliverables.ToArray(),
                obj.Length.ToString(),
                obj.Status.ToString(),
                obj.RunAtUtc.HasValue ? DateTime.SpecifyKind(obj.RunAtUtc.Value, DateTimeKind.Utc) : null,
                obj.EmailRecipients.ToArray(),
                obj.PublishToFeed,
                obj.OutOfScopeReason,
                obj.EstimatedCostUsd,
                obj.RunId,
                obj.BriefingId,
                obj.ConvertedTopicId,
                obj.ErrorMessage,
                obj.ConfirmedUtc.HasValue ? DateTime.SpecifyKind(obj.ConfirmedUtc.Value, DateTimeKind.Utc) : null,
                obj.CompletedUtc.HasValue ? DateTime.SpecifyKind(obj.CompletedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("ResearchRequest " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ResearchRequestQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(ResearchRequestQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ResearchRequestQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ResearchRequest>> EnumerateAsync(ResearchRequestFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ResearchRequestQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + ResearchRequestQueries.Columns + " FROM " + ResearchRequestQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<ResearchRequest> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<ResearchRequest>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(ResearchRequestFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ResearchRequestQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="ResearchRequestQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static ResearchRequest Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            ResearchRequest obj = new ResearchRequest();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.TopicId = (reader.IsDBNull(3) ? null : reader.GetString(3));
            obj.RequestText = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.Title = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Deliverables = (reader.IsDBNull(6) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(6)));
            obj.Length = Enum.Parse<DeliverableLengthEnum>(reader.GetString(7));
            obj.Status = Enum.Parse<ResearchRequestStatusEnum>(reader.GetString(8));
            obj.RunAtUtc = (reader.IsDBNull(9) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
            obj.EmailRecipients = (reader.IsDBNull(10) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(10)));
            obj.PublishToFeed = reader.GetBoolean(11);
            obj.OutOfScopeReason = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.EstimatedCostUsd = (reader.IsDBNull(13) ? (decimal?)null : reader.GetDecimal(13));
            obj.RunId = (reader.IsDBNull(14) ? null : reader.GetString(14));
            obj.BriefingId = (reader.IsDBNull(15) ? null : reader.GetString(15));
            obj.ConvertedTopicId = (reader.IsDBNull(16) ? null : reader.GetString(16));
            obj.ErrorMessage = (reader.IsDBNull(17) ? null : reader.GetString(17));
            obj.ConfirmedUtc = (reader.IsDBNull(18) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(18), DateTimeKind.Utc));
            obj.CompletedUtc = (reader.IsDBNull(19) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(20);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(21), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(22), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(ResearchRequestFilter filter, List<object?> args)
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
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(requesttext ILIKE $" + args.Count + " OR title ILIKE $" + args.Count + ")");
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
