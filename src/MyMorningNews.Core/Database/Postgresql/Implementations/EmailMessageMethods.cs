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
    /// PostgreSQL implementation of <see cref="IEmailMessageMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class EmailMessageMethods : PostgresqlMethodsBase, IEmailMessageMethods
    {
        #region Private-Members

        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nextAttemptUtc", "nextattemptutc" },
            { "sentUtc", "sentutc" },
            { "createdUtc", "createdutc" },
            { "lastUpdateUtc", "lastupdateutc" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public EmailMessageMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<EmailMessage> CreateAsync(EmailMessage obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateEmailMessageId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(EmailMessageQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.UserId,
                obj.Template.ToString(),
                obj.RunId,
                obj.BriefingId,
                obj.TopicId,
                obj.Recipient,
                obj.Subject,
                obj.Status.ToString(),
                obj.Provider.ToString(),
                obj.ProviderMessageId,
                obj.Attempts,
                obj.NextAttemptUtc.HasValue ? DateTime.SpecifyKind(obj.NextAttemptUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.LastError,
                obj.IdempotencyKey,
                obj.RenderedHtml,
                obj.RenderedText,
                obj.SentUtc.HasValue ? DateTime.SpecifyKind(obj.SentUtc.Value, DateTimeKind.Utc) : null,
                obj.FailedUtc.HasValue ? DateTime.SpecifyKind(obj.FailedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<EmailMessage?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<EmailMessage> rows = await QueryAsync(EmailMessageQueries.SelectById, Map, token, id, tenantId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<EmailMessage?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<EmailMessage> rows = await QueryAsync(EmailMessageQueries.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<EmailMessage> UpdateAsync(EmailMessage obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(EmailMessageQueries.Update, token,
                obj.Id,
                (object?)obj.TenantId,
                obj.UserId,
                obj.Template.ToString(),
                obj.RunId,
                obj.BriefingId,
                obj.TopicId,
                obj.Recipient,
                obj.Subject,
                obj.Status.ToString(),
                obj.Provider.ToString(),
                obj.ProviderMessageId,
                obj.Attempts,
                obj.NextAttemptUtc.HasValue ? DateTime.SpecifyKind(obj.NextAttemptUtc.Value, DateTimeKind.Utc) : null,
                obj.LockedUntilUtc.HasValue ? DateTime.SpecifyKind(obj.LockedUntilUtc.Value, DateTimeKind.Utc) : null,
                obj.LastError,
                obj.IdempotencyKey,
                obj.RenderedHtml,
                obj.RenderedText,
                obj.SentUtc.HasValue ? DateTime.SpecifyKind(obj.SentUtc.Value, DateTimeKind.Utc) : null,
                obj.FailedUtc.HasValue ? DateTime.SpecifyKind(obj.FailedUtc.Value, DateTimeKind.Utc) : null,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("EmailMessage " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(EmailMessageQueries.Delete, token, id, tenantId).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(EmailMessageQueries.Exists, r => 1, token, id, tenantId).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(EmailMessageQueries.DeleteUnscoped, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<EmailMessage>> EnumerateAsync(EmailMessageFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EmailMessageQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + EmailMessageQueries.Columns + " FROM " + EmailMessageQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<EmailMessage> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<EmailMessage>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(EmailMessageFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EmailMessageQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<List<EmailMessage>> ClaimDueAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, CancellationToken token = default)
        {
            string sql = "UPDATE emailmessages SET status = 'Sending', lockeduntilutc = $2, attempts = attempts + 1, lastupdateutc = $1 WHERE id IN (SELECT id FROM emailmessages WHERE status = 'Queued' " +
                "AND (nextattemptutc IS NULL OR nextattemptutc <= $1) ORDER BY createdutc FOR UPDATE SKIP LOCKED LIMIT $3) RETURNING " + EmailMessageQueries.Columns;
            return await QueryAsync(sql, Map, token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), DateTime.SpecifyKind(lockedUntilUtc, DateTimeKind.Utc), Math.Clamp(limit, 1, 200)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> RecoverAbandonedAsync(DateTime nowUtc, CancellationToken token = default)
        {
            return await ExecuteAsync("UPDATE emailmessages SET status = 'Queued', lockeduntilutc = NULL, lastupdateutc = $1 WHERE status = 'Sending' AND lockeduntilutc < $1", token, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> PruneAsync(DateTime beforeUtc, CancellationToken token = default)
        {
            DateTime cutoff = DateTime.SpecifyKind(beforeUtc, DateTimeKind.Utc);
            await ExecuteAsync("DELETE FROM emailactiontokens WHERE emailmessageid IN (SELECT id FROM emailmessages WHERE createdutc < $1 AND status NOT IN ('Queued', 'Sending'))", token, cutoff).ConfigureAwait(false);
            return await ExecuteAsync("DELETE FROM emailmessages WHERE createdutc < $1 AND status NOT IN ('Queued', 'Sending')", token, cutoff).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="EmailMessageQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static EmailMessage Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            EmailMessage obj = new EmailMessage();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.TenantId = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.UserId = (reader.IsDBNull(2) ? null : reader.GetString(2));
            obj.Template = Enum.Parse<EmailTemplateEnum>(reader.GetString(3));
            obj.RunId = (reader.IsDBNull(4) ? null : reader.GetString(4));
            obj.BriefingId = (reader.IsDBNull(5) ? null : reader.GetString(5));
            obj.TopicId = (reader.IsDBNull(6) ? null : reader.GetString(6));
            obj.Recipient = (reader.IsDBNull(7) ? String.Empty : reader.GetString(7));
            obj.Subject = (reader.IsDBNull(8) ? String.Empty : reader.GetString(8));
            obj.Status = Enum.Parse<EmailStatusEnum>(reader.GetString(9));
            obj.Provider = Enum.Parse<EmailProviderEnum>(reader.GetString(10));
            obj.ProviderMessageId = (reader.IsDBNull(11) ? null : reader.GetString(11));
            obj.Attempts = reader.GetInt32(12);
            obj.NextAttemptUtc = (reader.IsDBNull(13) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc));
            obj.LockedUntilUtc = (reader.IsDBNull(14) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc));
            obj.LastError = (reader.IsDBNull(15) ? null : reader.GetString(15));
            obj.IdempotencyKey = (reader.IsDBNull(16) ? String.Empty : reader.GetString(16));
            obj.RenderedHtml = (reader.IsDBNull(17) ? null : reader.GetString(17));
            obj.RenderedText = (reader.IsDBNull(18) ? null : reader.GetString(18));
            obj.SentUtc = (reader.IsDBNull(19) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc));
            obj.FailedUtc = (reader.IsDBNull(20) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(20), DateTimeKind.Utc));
            obj.Active = reader.GetBoolean(21);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(22), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(23), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(EmailMessageFilter filter, List<object?> args)
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
            if (filter.Template.HasValue)
            {
                args.Add(filter.Template.Value.ToString());
                clauses.Add("template = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.RunId))
            {
                args.Add(filter.RunId);
                clauses.Add("runid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.BriefingId))
            {
                args.Add(filter.BriefingId);
                clauses.Add("briefingid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.TopicId))
            {
                args.Add(filter.TopicId);
                clauses.Add("topicid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Recipient))
            {
                args.Add(filter.Recipient);
                clauses.Add("recipient = $" + args.Count);
            }
            if (filter.Status.HasValue)
            {
                args.Add(filter.Status.Value.ToString());
                clauses.Add("status = $" + args.Count);
            }
            if (filter.Provider.HasValue)
            {
                args.Add(filter.Provider.Value.ToString());
                clauses.Add("provider = $" + args.Count);
            }
            if (filter.Active.HasValue)
            {
                args.Add(filter.Active.Value);
                clauses.Add("active = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Search))
            {
                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");
                clauses.Add("(recipient ILIKE $" + args.Count + " OR subject ILIKE $" + args.Count + ")");
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
