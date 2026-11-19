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
    /// PostgreSQL implementation of <see cref="IEditionMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class EditionMethods : PostgresqlMethodsBase, IEditionMethods
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
        public EditionMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Edition> CreateAsync(Edition obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateEditionId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(EditionQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Name,
                obj.Description,
                obj.BriefingTemplateId,
                obj.VoiceProfileId,
                obj.AudioEnabled,
                obj.EmailEnabled,
                obj.Enabled,
                obj.FreshResearchBeforeCompose,
                obj.FreshResearchLeadMinutes,
                obj.LookbackMode.ToString(),
                obj.LookbackHours,
                obj.LastComposedUtc.HasValue ? DateTime.SpecifyKind(obj.LastComposedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<Edition?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Edition> rows = await QueryAsync(EditionQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Edition?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<Edition> rows = await QueryAsync(EditionQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<Edition> UpdateAsync(Edition obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(EditionQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Name,
                obj.Description,
                obj.BriefingTemplateId,
                obj.VoiceProfileId,
                obj.AudioEnabled,
                obj.EmailEnabled,
                obj.Enabled,
                obj.FreshResearchBeforeCompose,
                obj.FreshResearchLeadMinutes,
                obj.LookbackMode.ToString(),
                obj.LookbackHours,
                obj.LastComposedUtc.HasValue ? DateTime.SpecifyKind(obj.LastComposedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("Edition " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(EditionQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(EditionQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(EditionQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Edition>> EnumerateAsync(EditionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EditionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + EditionQueries.Columns + " FROM " + EditionQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<Edition> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<Edition>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(EditionFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EditionQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="EditionQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static Edition Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            Edition obj = new Edition();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.Name = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Description = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.BriefingTemplateId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.VoiceProfileId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.AudioEnabled = reader.GetBoolean(7);
            obj.EmailEnabled = reader.GetBoolean(8);
            obj.Enabled = reader.GetBoolean(9);
            obj.FreshResearchBeforeCompose = reader.GetBoolean(10);
            obj.FreshResearchLeadMinutes = reader.GetInt32(11);
            obj.LookbackMode = Enum.Parse<LookbackModeEnum>(reader.GetString(12));
            obj.LookbackHours = reader.GetInt32(13);
            obj.LastComposedUtc = (reader.IsDBNull(14) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(15);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(17), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(EditionFilter filter, List<object?> args)
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
            if (filter.Enabled.HasValue)
            {
                args.Add(filter.Enabled.Value);
                clauses.Add("enabled = $" + args.Count);
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
