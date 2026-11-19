namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using Pgvector;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IEmbeddingMethods"/> using the pgvector cosine distance operator.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class EmbeddingMethods : PostgresqlMethodsBase, IEmbeddingMethods
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        public EmbeddingMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        /// <inheritdoc />
        public async Task UpsertAsync(string tenantId, string ownerType, string ownerId, string? topicId, string? modelEndpointId, string model, float[] vector, string? contentHash, CancellationToken token = default)
        {
            if (vector == null || vector.Length == 0) throw new ArgumentException("The vector is empty.", nameof(vector));
            string sql = "INSERT INTO embeddings (id, tenantid, ownertype, ownerid, topicid, modelendpointid, model, dimensions, vector, contenthash, createdutc) " +
                "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11) ON CONFLICT (tenantid, ownertype, ownerid, model) DO UPDATE SET vector = EXCLUDED.vector, " +
                "dimensions = EXCLUDED.dimensions, contenthash = EXCLUDED.contenthash, topicid = EXCLUDED.topicid, modelendpointid = EXCLUDED.modelendpointid, createdutc = EXCLUDED.createdutc";
            await ExecuteAsync(sql, token, IdGenerator.GenerateEmbeddingId(), tenantId, ownerType, ownerId, (object?)topicId, (object?)modelEndpointId, model, vector.Length,
                new Vector(vector), (object?)contentHash, DateTime.UtcNow).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<EmbeddingMatch>> NearestAsync(string tenantId, string topicId, string ownerType, string model, float[] vector, DateTime sinceUtc, IEnumerable<string>? excludeOwnerIds, int limit, CancellationToken token = default)
        {
            if (vector == null || vector.Length == 0) return new List<EmbeddingMatch>();
            string[] exclude = (excludeOwnerIds ?? Enumerable.Empty<string>()).ToArray();
            string sql = "SELECT ownerid, 1 - (vector <=> $1) AS similarity FROM embeddings WHERE tenantid = $2 AND topicid = $3 AND ownertype = $4 AND model = $5 AND dimensions = $6 " +
                "AND createdutc >= $7 AND NOT (ownerid = ANY($8)) ORDER BY vector <=> $1 LIMIT $9";
            return await QueryAsync(sql, r => new EmbeddingMatch { OwnerId = r.GetString(0), Similarity = r.GetDouble(1) }, token,
                new Vector(vector), tenantId, topicId, ownerType, model, vector.Length, DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc), exclude, Math.Clamp(limit, 1, 200)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> DeleteForOwnerAsync(string tenantId, string ownerType, string ownerId, CancellationToken token = default)
        {
            return await ExecuteAsync("DELETE FROM embeddings WHERE tenantid = $1 AND ownertype = $2 AND ownerid = $3", token, tenantId, ownerType, ownerId).ConfigureAwait(false);
        }
    }
}
