namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Models;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="ITopicSlotNoticeMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class TopicSlotNoticeMethods : PostgresqlMethodsBase, ITopicSlotNoticeMethods
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public TopicSlotNoticeMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        /// <inheritdoc />
        public async Task<TopicSlotNotice?> ReadAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            return (await QueryAsync(TopicSlotNoticeQueries.Select, Map, token, tenantId, userId).ConfigureAwait(false)).FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> TryCreateAsync(TopicSlotNotice notice, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(notice);
            if (String.IsNullOrWhiteSpace(notice.TenantId)) throw new ArgumentNullException(nameof(notice), "TenantId is required.");
            if (String.IsNullOrWhiteSpace(notice.UserId)) throw new ArgumentNullException(nameof(notice), "UserId is required.");
            if (!notice.NotifiedUtc.HasValue) notice.NotifiedUtc = DateTime.UtcNow;
            int rows = await ExecuteAsync(TopicSlotNoticeQueries.Insert, token, notice.UserId, notice.TenantId, notice.OverLimit, notice.TopicLimit,
                DateTime.SpecifyKind(notice.NotifiedUtc.Value, DateTimeKind.Utc)).ConfigureAwait(false);
            return rows > 0;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            return await ExecuteAsync(TopicSlotNoticeQueries.Delete, token, tenantId, userId).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc />
        public async Task<List<TopicSlotNotice>> ListAccountsAsync(string? afterUserId, int limit, CancellationToken token = default)
        {
            return await QueryAsync(TopicSlotNoticeQueries.Accounts, Map, token, afterUserId ?? String.Empty, Math.Clamp(limit, 1, 5000)).ConfigureAwait(false);
        }

        private static TopicSlotNotice Map(NpgsqlDataReader r)
        {
            return new TopicSlotNotice
            {
                TenantId = r.GetString(0),
                UserId = r.GetString(1),
                OverLimit = Convert.ToInt64(r.GetValue(2)),
                TopicLimit = Convert.ToInt64(r.GetValue(3)),
                NotifiedUtc = r.IsDBNull(4) ? null : DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc)
            };
        }
    }
}
