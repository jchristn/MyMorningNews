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
    /// PostgreSQL implementation of <see cref="IFindingMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class FindingMethods : PostgresqlMethodsBase, IFindingMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "relevanceScore", "relevancescore" },
            { "rank", "rank" },
            { "publishedUtc", "publishedutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public FindingMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Finding> CreateAsync(Finding obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateFindingId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(FindingQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.RunId,
                obj.TopicId,
                obj.UserId,
                obj.DimensionId,
                obj.SourceItemId,
                obj.Title,
                obj.Summary,
                obj.WhyItMatters,
                obj.Category.ToString(),
                obj.RelevanceScore,
                obj.LlmScore,
                obj.FeedbackScore,
                obj.NoveltyScore,
                obj.SourceScore,
                obj.RecencyScore,
                obj.Tier.ToString(),
                obj.RelevanceRationale,
                obj.Confidence,
                obj.IsDuplicate,
                obj.DuplicateOfFindingId,
                obj.IsUpdateOfFindingId,
                obj.Rank,
                obj.UserRating,
                obj.PublishedUtc.HasValue ? DateTime.SpecifyKind(obj.PublishedUtc.Value, DateTimeKind.Utc) : null,
                obj.IsDryRun,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Finding?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Finding> rows = await QueryAsync(FindingQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Finding?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Finding> rows = await QueryAsync(FindingQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Finding> UpdateAsync(Finding obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(FindingQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.RunId,
                obj.TopicId,
                obj.UserId,
                obj.DimensionId,
                obj.SourceItemId,
                obj.Title,
                obj.Summary,
                obj.WhyItMatters,
                obj.Category.ToString(),
                obj.RelevanceScore,
                obj.LlmScore,
                obj.FeedbackScore,
                obj.NoveltyScore,
                obj.SourceScore,
                obj.RecencyScore,
                obj.Tier.ToString(),
                obj.RelevanceRationale,
                obj.Confidence,
                obj.IsDuplicate,
                obj.DuplicateOfFindingId,
                obj.IsUpdateOfFindingId,
                obj.Rank,
                obj.UserRating,
                obj.PublishedUtc.HasValue ? DateTime.SpecifyKind(obj.PublishedUtc.Value, DateTimeKind.Utc) : null,
                obj.IsDryRun,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Finding " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(FindingQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(FindingQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(FindingQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Finding>> EnumerateAsync(FindingFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + FindingQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + FindingQueries.Columns + " FROM " + FindingQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Finding> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Finding>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(FindingFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + FindingQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="FindingQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Finding Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Finding obj = new Finding();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.RunId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.TopicId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.UserId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.DimensionId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.SourceItemId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.Title = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.Summary = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.WhyItMatters = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.Category = Enum.Parse<FindingCategoryEnum>(reader.GetString(10));
            obj.RelevanceScore = reader.GetDouble(11);
            obj.LlmScore = reader.GetDouble(12);
            obj.FeedbackScore = reader.GetDouble(13);
            obj.NoveltyScore = reader.GetDouble(14);
            obj.SourceScore = reader.GetDouble(15);
            obj.RecencyScore = reader.GetDouble(16);
            obj.Tier = Enum.Parse<FindingTierEnum>(reader.GetString(17));
            obj.RelevanceRationale = (reader.IsDBNull(18) ? null : reader.GetString(18));
            obj.Confidence = reader.GetDouble(19);
            obj.IsDuplicate = reader.GetBoolean(20);
            obj.DuplicateOfFindingId = (reader.IsDBNull(21) ? null : reader.GetString(21));
            obj.IsUpdateOfFindingId = (reader.IsDBNull(22) ? null : reader.GetString(22));
            obj.Rank = reader.GetInt32(23);
            obj.UserRating = (reader.IsDBNull(24) ? null : reader.GetString(24));
            obj.PublishedUtc = (reader.IsDBNull(25) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(25), DateTimeKind.Utc));
            obj.IsDryRun = reader.GetBoolean(26);
            obj.Active = reader.GetBoolean(27);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(28), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(29), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(FindingFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (!String.IsNullOrWhiteSpace(filter.RunId))
            {
                args.Add(filter.RunId);
                clauses.Add("runid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.UserId))
            {
                args.Add(filter.UserId);
                clauses.Add("userid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.DimensionId))
            {
                args.Add(filter.DimensionId);
                clauses.Add("dimensionid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.SourceItemId))
            {
                args.Add(filter.SourceItemId);
                clauses.Add("sourceitemid = $" + args.Count);
            }
            if (filter.Category.HasValue)
            {
                args.Add(filter.Category.Value.ToString());
                clauses.Add("category = $" + args.Count);
            }
            if (filter.MinRelevanceScore.HasValue)
            {
                args.Add(filter.MinRelevanceScore.Value);
                clauses.Add("relevancescore >= $" + args.Count);
            }
            if (filter.ExcludeTier.HasValue)
            {
                args.Add(filter.ExcludeTier.Value.ToString());
                clauses.Add("tier <> $" + args.Count);
            }
            if (filter.Tier.HasValue)
            {
                args.Add(filter.Tier.Value.ToString());
                clauses.Add("tier = $" + args.Count);
            }
            if (filter.IsDuplicate.HasValue)
            {
                args.Add(filter.IsDuplicate.Value);
                clauses.Add("isduplicate = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.UserRating))
            {
                args.Add(filter.UserRating);
                clauses.Add("userrating = $" + args.Count);
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
                clauses.Add("(title ILIKE $" + args.Count + " OR summary ILIKE $" + args.Count + ")");
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
