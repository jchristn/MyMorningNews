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
    /// PostgreSQL implementation of <see cref="IBriefingDeltaMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class BriefingDeltaMethods : PostgresqlMethodsBase, IBriefingDeltaMethods
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public BriefingDeltaMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        /// <inheritdoc />
        public async Task<BriefingDelta> CreateAsync(BriefingDelta obj, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.GenerateBriefingDeltaId();
            DateTime now = DateTime.UtcNow;
            obj.CreatedUtc = now;
            obj.LastUpdateUtc = now;
            await ExecuteAsync(BriefingDeltaQueries.Insert, token,
                obj.Id,
                obj.TenantId,
                obj.BriefingId,
                obj.Kind.ToString(),
                obj.Title,
                obj.Summary,
                obj.FindingId,
                obj.PredictionId,
                obj.BaselineBriefingId,
                obj.BaselineItemId,
                obj.SortOrder,
                obj.Active,
                DateTime.SpecifyKind(obj.CreatedUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(obj.LastUpdateUtc, DateTimeKind.Utc)).ConfigureAwait(false);
            return obj;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<BriefingDelta>> EnumerateAsync(BriefingDeltaFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?> { filter.TenantId };
            List<string> clauses = new List<string> { "tenantid = $1" };
            if (!String.IsNullOrWhiteSpace(filter.BriefingId))
            {
                args.Add(filter.BriefingId);
                clauses.Add("briefingid = $" + args.Count);
            }
            if (filter.Kind.HasValue)
            {
                args.Add(filter.Kind.Value.ToString());
                clauses.Add("kind = $" + args.Count);
            }
            string where = " WHERE " + String.Join(" AND ", clauses);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + BriefingDeltaQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + BriefingDeltaQueries.Columns + " FROM " + BriefingDeltaQueries.Table + where + " ORDER BY sortorder, id LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<BriefingDelta> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<BriefingDelta>.Create(items, filter, counts.FirstOrDefault(), sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<int> DeleteByBriefingAsync(string tenantId, string briefingId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(briefingId)) throw new ArgumentNullException(nameof(briefingId));
            return await ExecuteAsync(BriefingDeltaQueries.DeleteByBriefing, token, tenantId, briefingId).ConfigureAwait(false);
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="BriefingDeltaQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        /// <exception cref="ArgumentNullException">reader is null.</exception>
        public static BriefingDelta Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            BriefingDelta obj = new BriefingDelta();
            obj.Id = reader.GetString(0);
            obj.TenantId = reader.GetString(1);
            obj.BriefingId = reader.GetString(2);
            obj.Kind = Enum.Parse<BriefingDeltaKindEnum>(reader.GetString(3));
            obj.Title = reader.GetString(4);
            obj.Summary = reader.IsDBNull(5) ? null : reader.GetString(5);
            obj.FindingId = reader.IsDBNull(6) ? null : reader.GetString(6);
            obj.PredictionId = reader.IsDBNull(7) ? null : reader.GetString(7);
            obj.BaselineBriefingId = reader.IsDBNull(8) ? null : reader.GetString(8);
            obj.BaselineItemId = reader.IsDBNull(9) ? null : reader.GetString(9);
            obj.SortOrder = reader.GetInt32(10);
            obj.Active = reader.GetBoolean(11);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc);
            return obj;
        }
    }
}
