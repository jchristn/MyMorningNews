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
    /// PostgreSQL implementation of <see cref="ILlmCallMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class LlmCallMethods : PostgresqlMethodsBase, ILlmCallMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "durationMs", "durationms" },
            { "timeToFirstTokenMs", "timetofirsttokenms" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public LlmCallMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<LlmCall> CreateAsync(LlmCall obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateLlmCallId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(LlmCallQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.Task.ToString(),
                obj.RunId,
                obj.RefinementSessionId,
                obj.BriefingId,
                obj.ModelEndpointId,
                obj.Provider,
                obj.Model,
                obj.PromptKey,
                obj.PromptTemplateVersionId,
                obj.UserId,
                obj.TopicId,
                obj.Streamed,
                obj.Attempts,
                obj.InputTokens,
                obj.CachedInputTokens,
                obj.CacheWriteTokens,
                obj.OutputTokens,
                obj.ReasoningTokens,
                obj.TotalTokens,
                obj.DurationMs,
                obj.TimeToFirstTokenMs,
                obj.StreamingDurationMs,
                obj.ChunkCount,
                obj.OutputTokensPerSecond,
                obj.ToolCallCount,
                obj.ToolNames.ToArray(),
                obj.Success,
                obj.StatusCode,
                obj.FinishReason,
                obj.ErrorMessage,
                obj.StructuredOutputRepairs,
                obj.InputCostUsd,
                obj.OutputCostUsd,
                obj.EstimatedCostUsd,
                obj.IsDryRun,
                obj.PromptText,
                obj.ResponseText,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<LlmCall?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<LlmCall> rows = await QueryAsync(LlmCallQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<LlmCall?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<LlmCall> rows = await QueryAsync(LlmCallQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<LlmCall> UpdateAsync(LlmCall obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(LlmCallQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.Task.ToString(),
                obj.RunId,
                obj.RefinementSessionId,
                obj.BriefingId,
                obj.ModelEndpointId,
                obj.Provider,
                obj.Model,
                obj.PromptKey,
                obj.PromptTemplateVersionId,
                obj.UserId,
                obj.TopicId,
                obj.Streamed,
                obj.Attempts,
                obj.InputTokens,
                obj.CachedInputTokens,
                obj.CacheWriteTokens,
                obj.OutputTokens,
                obj.ReasoningTokens,
                obj.TotalTokens,
                obj.DurationMs,
                obj.TimeToFirstTokenMs,
                obj.StreamingDurationMs,
                obj.ChunkCount,
                obj.OutputTokensPerSecond,
                obj.ToolCallCount,
                obj.ToolNames.ToArray(),
                obj.Success,
                obj.StatusCode,
                obj.FinishReason,
                obj.ErrorMessage,
                obj.StructuredOutputRepairs,
                obj.InputCostUsd,
                obj.OutputCostUsd,
                obj.EstimatedCostUsd,
                obj.IsDryRun,
                obj.PromptText,
                obj.ResponseText,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("LlmCall " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(LlmCallQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(LlmCallQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(LlmCallQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<LlmCall>> EnumerateAsync(LlmCallFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + LlmCallQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + LlmCallQueries.Columns + " FROM " + LlmCallQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<LlmCall> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<LlmCall>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(LlmCallFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + LlmCallQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<int> ClearPayloadsAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            return await ExecuteAsync("UPDATE llmcalls SET prompttext = NULL, responsetext = NULL WHERE createdutc < $1 AND (prompttext IS NOT NULL OR responsetext IS NOT NULL)", token, DateTime.SpecifyKind(olderThanUtc, DateTimeKind.Utc)).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="LlmCallQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static LlmCall Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            LlmCall obj = new LlmCall();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.Task = Enum.Parse<ModelTaskEnum>(reader.GetString(2));
            obj.RunId = (reader.IsDBNull(3) ? null : reader.GetString(3));
            obj.RefinementSessionId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.BriefingId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.ModelEndpointId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.Provider = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.Model = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.PromptKey = (reader.IsDBNull(9) ? null : reader.GetString(9));
            obj.PromptTemplateVersionId = (reader.IsDBNull(10) ? null : reader.GetString(10));
            obj.UserId = (reader.IsDBNull(11) ? null : reader.GetString(11));
            obj.TopicId = (reader.IsDBNull(12) ? null : reader.GetString(12));
            obj.Streamed = reader.GetBoolean(13);
            obj.Attempts = reader.GetInt32(14);
            obj.InputTokens = reader.GetInt32(15);
            obj.CachedInputTokens = reader.GetInt32(16);
            obj.CacheWriteTokens = reader.GetInt32(17);
            obj.OutputTokens = reader.GetInt32(18);
            obj.ReasoningTokens = reader.GetInt32(19);
            obj.TotalTokens = reader.GetInt32(20);
            obj.DurationMs = reader.GetDouble(21);
            obj.TimeToFirstTokenMs = (reader.IsDBNull(22) ? (double?)null : reader.GetDouble(22));
            obj.StreamingDurationMs = (reader.IsDBNull(23) ? (double?)null : reader.GetDouble(23));
            obj.ChunkCount = reader.GetInt32(24);
            obj.OutputTokensPerSecond = (reader.IsDBNull(25) ? (double?)null : reader.GetDouble(25));
            obj.ToolCallCount = reader.GetInt32(26);
            obj.ToolNames = (reader.IsDBNull(27) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(27)));
            obj.Success = reader.GetBoolean(28);
            obj.StatusCode = (reader.IsDBNull(29) ? (int?)null : reader.GetInt32(29));
            obj.FinishReason = (reader.IsDBNull(30) ? null : reader.GetString(30));
            obj.ErrorMessage = (reader.IsDBNull(31) ? null : reader.GetString(31));
            obj.StructuredOutputRepairs = reader.GetInt32(32);
            obj.InputCostUsd = (reader.IsDBNull(33) ? (decimal?)null : reader.GetDecimal(33));
            obj.OutputCostUsd = (reader.IsDBNull(34) ? (decimal?)null : reader.GetDecimal(34));
            obj.EstimatedCostUsd = (reader.IsDBNull(35) ? (decimal?)null : reader.GetDecimal(35));
            obj.IsDryRun = reader.GetBoolean(36);
            obj.PromptText = (reader.IsDBNull(37) ? null : reader.GetString(37));
            obj.ResponseText = (reader.IsDBNull(38) ? null : reader.GetString(38));
            obj.Active = reader.GetBoolean(39);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(40), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(41), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(LlmCallFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (filter.Task.HasValue)
            {
                args.Add(filter.Task.Value.ToString());
                clauses.Add("task = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.RunId))
            {
                args.Add(filter.RunId);
                clauses.Add("runid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.RefinementSessionId))
            {
                args.Add(filter.RefinementSessionId);
                clauses.Add("refinementsessionid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.BriefingId))
            {
                args.Add(filter.BriefingId);
                clauses.Add("briefingid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.ModelEndpointId))
            {
                args.Add(filter.ModelEndpointId);
                clauses.Add("modelendpointid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Model))
            {
                args.Add(filter.Model);
                clauses.Add("model = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.PromptKey))
            {
                args.Add(filter.PromptKey);
                clauses.Add("promptkey = $" + args.Count);
            }
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
            if (filter.Success.HasValue)
            {
                args.Add(filter.Success.Value);
                clauses.Add("success = $" + args.Count);
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
