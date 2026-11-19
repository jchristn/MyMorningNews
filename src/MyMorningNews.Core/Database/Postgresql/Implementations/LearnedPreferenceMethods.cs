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
    /// PostgreSQL implementation of <see cref="ILearnedPreferenceMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class LearnedPreferenceMethods : PostgresqlMethodsBase, ILearnedPreferenceMethods
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
        public LearnedPreferenceMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<LearnedPreference> CreateAsync(LearnedPreference obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateLearnedPreferenceId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(LearnedPreferenceQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.TopicId,
                obj.Kind.ToString(),
                obj.Statement,
                obj.Status.ToString(),
                obj.Origin.ToString(),
                obj.SupportCount,
                obj.Confidence,
                obj.LastEvidenceUtc.HasValue ? DateTime.SpecifyKind(obj.LastEvidenceUtc.Value, DateTimeKind.Utc) : null,
                obj.DecidedUtc.HasValue ? DateTime.SpecifyKind(obj.DecidedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<LearnedPreference?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<LearnedPreference> rows = await QueryAsync(LearnedPreferenceQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<LearnedPreference?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<LearnedPreference> rows = await QueryAsync(LearnedPreferenceQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<LearnedPreference> UpdateAsync(LearnedPreference obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(LearnedPreferenceQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.TopicId,
                obj.Kind.ToString(),
                obj.Statement,
                obj.Status.ToString(),
                obj.Origin.ToString(),
                obj.SupportCount,
                obj.Confidence,
                obj.LastEvidenceUtc.HasValue ? DateTime.SpecifyKind(obj.LastEvidenceUtc.Value, DateTimeKind.Utc) : null,
                obj.DecidedUtc.HasValue ? DateTime.SpecifyKind(obj.DecidedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("LearnedPreference " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(LearnedPreferenceQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(LearnedPreferenceQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(LearnedPreferenceQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<LearnedPreference>> EnumerateAsync(LearnedPreferenceFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + LearnedPreferenceQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + LearnedPreferenceQueries.Columns + " FROM " + LearnedPreferenceQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<LearnedPreference> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<LearnedPreference>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(LearnedPreferenceFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + LearnedPreferenceQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="LearnedPreferenceQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static LearnedPreference Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            LearnedPreference obj = new LearnedPreference();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.TopicId = (reader.IsDBNull(3) ? null : reader.GetString(3));
            obj.Kind = Enum.Parse<PreferenceKindEnum>(reader.GetString(4));
            obj.Statement = (reader.IsDBNull(5) ? String.Empty : reader.GetString(5));
            obj.Status = Enum.Parse<PreferenceStatusEnum>(reader.GetString(6));
            obj.Origin = Enum.Parse<PreferenceOriginEnum>(reader.GetString(7));
            obj.SupportCount = reader.GetInt32(8);
            obj.Confidence = reader.GetDouble(9);
            obj.LastEvidenceUtc = (reader.IsDBNull(10) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc));
            obj.DecidedUtc = (reader.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(12);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(LearnedPreferenceFilter filter, List<object?> args)
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
            if (filter.Origin.HasValue)
            {
                args.Add(filter.Origin.Value.ToString());
                clauses.Add("origin = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(statement ILIKE $" + args.Count + ")");
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
