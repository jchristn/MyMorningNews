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
    /// PostgreSQL implementation of <see cref="IBillingEventMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class BillingEventMethods : PostgresqlMethodsBase, IBillingEventMethods
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
        public BillingEventMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        #endregion

        #region Public-Methods
        /// <inheritdoc />
        public async Task EnqueueAsync(BillingEvent item, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(item);
            await ExecuteAsync(@"INSERT INTO billingevents (" + BillingEventQueries.Columns + @")
VALUES ($1,$2,$3,$4,$5,'Pending',0,now(),NULL,TRUE,now(),now())
ON CONFLICT (provider,environment,eventid) DO NOTHING", token, item.Id, item.Provider, item.Environment, item.EventId, item.ProviderId).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<BillingEvent?> ClaimAsync(CancellationToken token = default)
        {
            List<BillingEvent> rows = await QueryAsync(@"UPDATE billingevents SET status='Processing', attempts=attempts+1, retryutc=now()+interval '5 minutes',lastupdateutc=now()
WHERE id=(SELECT id FROM billingevents WHERE active AND status <> 'Completed' AND retryutc <= now() ORDER BY retryutc,createdutc FOR UPDATE SKIP LOCKED LIMIT 1)
RETURNING " + BillingEventQueries.Columns, Map, token).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> FinishClaimAsync(BillingEvent item, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Status != "Completed" && item.Status != "Pending") throw new ArgumentException("A claim must complete or retry.", nameof(item));
            return await ExecuteAsync(@"UPDATE billingevents SET status=$3,retryutc=$4,error=$5,lastupdateutc=now()
WHERE id=$1 AND attempts=$2 AND status='Processing' AND active", token, item.Id, item.Attempts, item.Status,
                DateTime.SpecifyKind(item.RetryUtc, DateTimeKind.Utc), item.Error).ConfigureAwait(false) == 1;
        }


        /// <inheritdoc />
        public async Task<BillingEvent> CreateAsync(BillingEvent obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateBillingEventId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(BillingEventQueries.Insert, token,
                obj.Id,
                obj.Provider,
                obj.Environment,
                obj.EventId,
                obj.ProviderId,
                obj.Status,
                obj.Attempts,
                DateTime.SpecifyKind(obj.RetryUtc, DateTimeKind.Utc),
                obj.Error,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<BillingEvent?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<BillingEvent> rows = await QueryAsync(BillingEventQueries.SelectById, Map, token, id).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<BillingEvent> UpdateAsync(BillingEvent obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            obj.LastUpdateUtc = DateTime.UtcNow;
            int affected = await ExecuteAsync(BillingEventQueries.Update, token,
                obj.Id,
                obj.Provider,
                obj.Environment,
                obj.EventId,
                obj.ProviderId,
                obj.Status,
                obj.Attempts,
                DateTime.SpecifyKind(obj.RetryUtc, DateTimeKind.Utc),
                obj.Error,
                obj.Active,
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            if (affected < 1) throw new KeyNotFoundException("BillingEvent " + obj.Id + " was not found.");
            return obj;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            int affected = await ExecuteAsync(BillingEventQueries.Delete, token, id).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            List<int> rows = await QueryAsync(BillingEventQueries.Exists, r => 1, token, id).ConfigureAwait(false);
            return rows.Count > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<BillingEvent>> EnumerateAsync(BillingEventFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "createdutc";
            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BillingEventQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            long total = counts.FirstOrDefault();
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + BillingEventQueries.Columns + " FROM " + BillingEventQueries.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<BillingEvent> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<BillingEvent>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<long> CountAsync(BillingEventFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            List<object?> args = new List<object?>();
            string where = BuildWhere(filter, args);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BillingEventQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            return counts.FirstOrDefault();
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="BillingEventQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        public static BillingEvent Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            BillingEvent obj = new BillingEvent();
            obj.Id = (reader.IsDBNull(0) ? String.Empty : reader.GetString(0));
            obj.Provider = (reader.IsDBNull(1) ? String.Empty : reader.GetString(1));
            obj.Environment = (reader.IsDBNull(2) ? String.Empty : reader.GetString(2));
            obj.EventId = (reader.IsDBNull(3) ? String.Empty : reader.GetString(3));
            obj.ProviderId = (reader.IsDBNull(4) ? String.Empty : reader.GetString(4));
            obj.Status = (reader.IsDBNull(5) ? String.Empty : reader.GetString(5));
            obj.Attempts = reader.GetInt32(6);
            obj.RetryUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);
            obj.Error = (reader.IsDBNull(8) ? null : reader.GetString(8));
            obj.Active = reader.GetBoolean(9);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc);
            return obj;
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(BillingEventFilter filter, List<object?> args)
        {
            List<string> clauses = new List<string>();
            if (!String.IsNullOrWhiteSpace(filter.Provider))
            {
                args.Add(filter.Provider);
                clauses.Add("provider = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Environment))
            {
                args.Add(filter.Environment);
                clauses.Add("environment = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.EventId))
            {
                args.Add(filter.EventId);
                clauses.Add("eventid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.Status))
            {
                args.Add(filter.Status);
                clauses.Add("status = $" + args.Count);
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
