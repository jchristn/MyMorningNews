namespace MyMorningNews.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Embedding vectors stored with pgvector. Vectors carry their model and dimensions, so several embedding models can
    /// coexist and only same-model, same-dimension vectors are compared.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IEmbeddingMethods
    {
        /// <summary>
        /// Insert or replace the vector for an owner and model.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="ownerType">Owner type (Finding, SourceItem, Feedback, Prediction).</param>
        /// <param name="ownerId">Owner ID.</param>
        /// <param name="topicId">Topic, when any.</param>
        /// <param name="modelEndpointId">Endpoint that produced it.</param>
        /// <param name="model">Embedding model.</param>
        /// <param name="vector">Vector.</param>
        /// <param name="contentHash">Hash of the embedded text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentException">The vector is empty.</exception>
        Task UpsertAsync(string tenantId, string ownerType, string ownerId, string? topicId, string? modelEndpointId, string model, float[] vector, string? contentHash, CancellationToken token = default);

        /// <summary>
        /// Nearest vectors in a topic by cosine similarity, same owner type, model, and dimensions.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="ownerType">Owner type.</param>
        /// <param name="model">Embedding model.</param>
        /// <param name="vector">Query vector.</param>
        /// <param name="sinceUtc">Only vectors created at or after this time.</param>
        /// <param name="excludeOwnerIds">Owners to skip (for example the candidate itself).</param>
        /// <param name="limit">Maximum matches.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matches, most similar first.</returns>
        Task<List<EmbeddingMatch>> NearestAsync(string tenantId, string topicId, string ownerType, string model, float[] vector, DateTime sinceUtc, IEnumerable<string>? excludeOwnerIds, int limit, CancellationToken token = default);

        /// <summary>
        /// Delete the vectors of an owner.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="ownerType">Owner type.</param>
        /// <param name="ownerId">Owner ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows deleted.</returns>
        Task<int> DeleteForOwnerAsync(string tenantId, string ownerType, string ownerId, CancellationToken token = default);
    }
}
