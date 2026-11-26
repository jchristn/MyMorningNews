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
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IEditionPublicationMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class EditionPublicationMethods : PostgresqlMethodsBase, IEditionPublicationMethods
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public EditionPublicationMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<EditionPublication>> EnumerateAsync(EditionPublicationFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");
            Stopwatch sw = Stopwatch.StartNew();
            List<object?> args = new List<object?> { filter.TenantId };
            List<string> clauses = new List<string> { "tenantid = $1" };
            if (!String.IsNullOrWhiteSpace(filter.EditionId))
            {
                args.Add(filter.EditionId);
                clauses.Add("editionid = $" + args.Count);
            }
            if (!String.IsNullOrWhiteSpace(filter.PublicationId))
            {
                args.Add(filter.PublicationId);
                clauses.Add("publicationid = $" + args.Count);
            }
            string where = " WHERE " + String.Join(" AND ", clauses);
            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + EditionPublicationQueries.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);
            args.Add(filter.PageSize);
            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);
            string sql = "SELECT " + EditionPublicationQueries.Columns + " FROM " + EditionPublicationQueries.Table + where + " ORDER BY sortorder, id LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;
            List<EditionPublication> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);
            return EnumerationResult<EditionPublication>.Create(items, filter, counts.FirstOrDefault(), sw.Elapsed.TotalMilliseconds);
        }

        /// <inheritdoc />
        public async Task<List<EditionPublication>> ReplaceAsync(string tenantId, string editionId, List<string> publicationIds, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(editionId)) throw new ArgumentNullException(nameof(editionId));
            ArgumentNullException.ThrowIfNull(publicationIds);
            string[] ids = publicationIds.ToArray();
            await ExecuteAsync("DELETE FROM editionpublications WHERE tenantid = $1 AND editionid = $2 AND publicationid <> ALL($3::text[])", token, tenantId, editionId, ids).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < ids.Length; i++)
            {
                await ExecuteAsync(EditionPublicationQueries.Insert + " ON CONFLICT (editionid, publicationid) DO UPDATE SET sortorder = EXCLUDED.sortorder, active = true, lastupdateutc = EXCLUDED.lastupdateutc", token,
                    IdGenerator.GenerateEditionPublicationId(), tenantId, editionId, ids[i], i, true, now, now).ConfigureAwait(false);
            }

            EditionPublicationFilter filter = new EditionPublicationFilter { TenantId = tenantId, EditionId = editionId, PageSize = 1000 };
            return (await EnumerateAsync(filter, token).ConfigureAwait(false)).Objects;
        }

        /// <summary>
        /// Map a data reader row (columns in <see cref="EditionPublicationQueries.Columns"/> order) to a model.
        /// </summary>
        /// <param name="reader">Reader positioned on a row.</param>
        /// <returns>Model.</returns>
        /// <exception cref="ArgumentNullException">reader is null.</exception>
        public static EditionPublication Map(NpgsqlDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            EditionPublication obj = new EditionPublication();
            obj.Id = reader.GetString(0);
            obj.TenantId = reader.GetString(1);
            obj.EditionId = reader.GetString(2);
            obj.PublicationId = reader.GetString(3);
            obj.SortOrder = reader.GetInt32(4);
            obj.Active = reader.GetBoolean(5);
            obj.CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc);
            obj.LastUpdateUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc);
            return obj;
        }
    }
}
