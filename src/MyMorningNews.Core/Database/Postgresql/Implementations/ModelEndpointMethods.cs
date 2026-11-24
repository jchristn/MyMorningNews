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
    /// PostgreSQL implementation of <see cref="IModelEndpointMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class ModelEndpointMethods : PostgresqlMethodsBase, IModelEndpointMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "name", "name" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public ModelEndpointMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ModelEndpoint> CreateAsync(ModelEndpoint obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateModelEndpointId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(ModelEndpointQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.Name,
                obj.Provider.ToString(),
                obj.Endpoint,
                obj.CredentialId,
                obj.Model,
                obj.EmbeddingModel,
                obj.EmbeddingDimensions,
                obj.SupportsChat,
                obj.SupportsTools,
                obj.SupportsEmbeddings,
                obj.Temperature,
                obj.MaxTokens,
                (object?)obj.ReasoningEffort?.ToString(),
                obj.ContextLength,
                obj.TimeoutMs,
                obj.InputCostPerMTok,
                obj.OutputCostPerMTok,
                obj.CachedInputCostPerMTok,
                obj.CacheWriteCostPerMTok,
                obj.LastValidatedUtc.HasValue ? DateTime.SpecifyKind(obj.LastValidatedUtc.Value, DateTimeKind.Utc) : null,
                obj.LastValidationResult,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.LastValidationSucceeded,
                (object?)obj.AuthMethod?.ToString()).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<ModelEndpoint?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<ModelEndpoint> rows = await QueryAsync(ModelEndpointQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<ModelEndpoint?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<ModelEndpoint> rows = await QueryAsync(ModelEndpointQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<ModelEndpoint> UpdateAsync(ModelEndpoint obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(ModelEndpointQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.Name,
                obj.Provider.ToString(),
                obj.Endpoint,
                obj.CredentialId,
                obj.Model,
                obj.EmbeddingModel,
                obj.EmbeddingDimensions,
                obj.SupportsChat,
                obj.SupportsTools,
                obj.SupportsEmbeddings,
                obj.Temperature,
                obj.MaxTokens,
                (object?)obj.ReasoningEffort?.ToString(),
                obj.ContextLength,
                obj.TimeoutMs,
                obj.InputCostPerMTok,
                obj.OutputCostPerMTok,
                obj.CachedInputCostPerMTok,
                obj.CacheWriteCostPerMTok,
                obj.LastValidatedUtc.HasValue ? DateTime.SpecifyKind(obj.LastValidatedUtc.Value, DateTimeKind.Utc) : null,
                obj.LastValidationResult,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc),
                obj.LastValidationSucceeded,
                (object?)obj.AuthMethod?.ToString()).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("ModelEndpoint " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ModelEndpointQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(ModelEndpointQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(ModelEndpointQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ModelEndpoint>> EnumerateAsync(ModelEndpointFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ModelEndpointQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + ModelEndpointQueries.Columns + " FROM " + ModelEndpointQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<ModelEndpoint> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<ModelEndpoint>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(ModelEndpointFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ModelEndpointQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="ModelEndpointQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static ModelEndpoint Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            ModelEndpoint obj = new ModelEndpoint();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.Name = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Provider = Enum.Parse<ModelProviderEnum>(reader.GetString(3));
            obj.Endpoint = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.CredentialId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.Model = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.EmbeddingModel = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.EmbeddingDimensions = (reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8));
            obj.SupportsChat = reader.GetBoolean(9);
            obj.SupportsTools = (reader.IsDBNull(10) ? (bool?)null : reader.GetBoolean(10));
            obj.SupportsEmbeddings = reader.GetBoolean(11);
            obj.Temperature = (reader.IsDBNull(12) ? (double?)null : reader.GetDouble(12));
            obj.MaxTokens = reader.GetInt32(13);
            obj.ReasoningEffort = (reader.IsDBNull(14) ? null : Enum.Parse<ReasoningEffortEnum>(reader.GetString(14)));
            obj.ContextLength = (reader.IsDBNull(15) ? (int?)null : reader.GetInt32(15));
            obj.TimeoutMs = reader.GetInt32(16);
            obj.InputCostPerMTok = (reader.IsDBNull(17) ? (decimal?)null : reader.GetDecimal(17));
            obj.OutputCostPerMTok = (reader.IsDBNull(18) ? (decimal?)null : reader.GetDecimal(18));
            obj.CachedInputCostPerMTok = (reader.IsDBNull(19) ? (decimal?)null : reader.GetDecimal(19));
            obj.CacheWriteCostPerMTok = (reader.IsDBNull(20) ? (decimal?)null : reader.GetDecimal(20));
            obj.LastValidatedUtc = (reader.IsDBNull(21) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(21), DateTimeKind.Utc));
            obj.LastValidationResult = (reader.IsDBNull(22) ? null : reader.GetString(22));
            obj.Active = reader.GetBoolean(23);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(24), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(25), DateTimeKind.Utc);
            obj.LastValidationSucceeded = (reader.IsDBNull(26) ? (bool?)null : reader.GetBoolean(26));
            obj.AuthMethod = (reader.IsDBNull(27) ? null : Enum.Parse<ModelAuthMethodEnum>(reader.GetString(27)));
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(ModelEndpointFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            args.Add(filter.TenantId);
            clauses.Add("tenantid = $" + args.Count);
            if (filter.Provider.HasValue)
            {
                args.Add(filter.Provider.Value.ToString());
                clauses.Add("provider = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.CredentialId))
            {
                args.Add(filter.CredentialId);
                clauses.Add("credentialid = $" + args.Count);
            }
            if (filter.SupportsChat.HasValue)
            {
                args.Add(filter.SupportsChat.Value);
                clauses.Add("supportschat = $" + args.Count);
            }
            if (filter.SupportsEmbeddings.HasValue)
            {
                args.Add(filter.SupportsEmbeddings.Value);
                clauses.Add("supportsembeddings = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(name ILIKE $" + args.Count + ")");
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
