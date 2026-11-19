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
    /// PostgreSQL implementation of <see cref="IEmailActionTokenMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class EmailActionTokenMethods : PostgresqlMethodsBase, IEmailActionTokenMethods
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
        public EmailActionTokenMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<EmailActionToken> CreateAsync(EmailActionToken obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateEmailActionTokenId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(EmailActionTokenQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.EmailMessageId,
                obj.Kind.ToString(),
                obj.TargetType.ToString(),
                obj.TargetId,
                obj.TopicId,
                obj.Recipient,
                obj.AllowedActions.ToArray(),
                obj.TokenHash,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.OpenCount,
                obj.SubmitCount,
                obj.LastUsedUtc.HasValue ? DateTime.SpecifyKind(obj.LastUsedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<EmailActionToken?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<EmailActionToken> rows = await QueryAsync(EmailActionTokenQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<EmailActionToken?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<EmailActionToken> rows = await QueryAsync(EmailActionTokenQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<EmailActionToken> UpdateAsync(EmailActionToken obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(EmailActionTokenQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.EmailMessageId,
                obj.Kind.ToString(),
                obj.TargetType.ToString(),
                obj.TargetId,
                obj.TopicId,
                obj.Recipient,
                obj.AllowedActions.ToArray(),
                obj.TokenHash,
                DateTime.SpecifyKind(obj.ExpiresUtc, DateTimeKind.Utc),
                obj.RevokedUtc.HasValue ? DateTime.SpecifyKind(obj.RevokedUtc.Value, DateTimeKind.Utc) : null,
                obj.OpenCount,
                obj.SubmitCount,
                obj.LastUsedUtc.HasValue ? DateTime.SpecifyKind(obj.LastUsedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("EmailActionToken " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(EmailActionTokenQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(EmailActionTokenQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(EmailActionTokenQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<EmailActionToken>> EnumerateAsync(EmailActionTokenFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EmailActionTokenQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + EmailActionTokenQueries.Columns + " FROM " + EmailActionTokenQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<EmailActionToken> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<EmailActionToken>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(EmailActionTokenFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EmailActionTokenQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<EmailActionToken?> ReadByHashAsync(string tokenHash, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tokenHash)) return null;
            List<EmailActionToken> rows = await QueryAsync("SELECT " + EmailActionTokenQueries.Columns + " FROM emailactiontokens WHERE tokenhash = $1 LIMIT 1", Map, token, tokenHash).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="EmailActionTokenQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static EmailActionToken Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            EmailActionToken obj = new EmailActionToken();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.EmailMessageId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.Kind = Enum.Parse<ActionTokenKindEnum>(reader.GetString(4));
            obj.TargetType = Enum.Parse<FeedbackTargetTypeEnum>(reader.GetString(5));
            obj.TargetId = (reader.IsDBNull(6) ? String.Empty : reader.GetString(6));
            obj.TopicId = (reader.IsDBNull(7) ? null : reader.GetString(7));
            obj.Recipient = (reader.IsDBNull(8) ? String.Empty : reader.GetString(8));
            obj.AllowedActions = (reader.IsDBNull(9) ? new List<string>() : new List<string>(reader.GetFieldValue<string[]>(9)));
            obj.TokenHash = (reader.IsDBNull(10) ? String.Empty : reader.GetString(10));
            obj.ExpiresUtc = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
            obj.RevokedUtc = (reader.IsDBNull(12) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc));
            obj.OpenCount = reader.GetInt32(13);
            obj.SubmitCount = reader.GetInt32(14);
            obj.LastUsedUtc = (reader.IsDBNull(15) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(16);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(17), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(18), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(EmailActionTokenFilter filter, List<object?> args)
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
            if (!String.IsNullOrWhiteSpace(filter.EmailMessageId))
            {
                args.Add(filter.EmailMessageId);
                clauses.Add("emailmessageid = $" + args.Count);
            }
            if (filter.Kind.HasValue)
            {
                args.Add(filter.Kind.Value.ToString());
                clauses.Add("kind = $" + args.Count);
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
