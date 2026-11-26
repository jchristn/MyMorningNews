namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Downgrade retention (plan section 8.4): reports which custom topics are inside the plan's limit and saves the
    /// owner's keep order. The order is used by the run gate, so a choice made before a downgrade takes effect applies
    /// when the limit shrinks. Nothing is deleted or archived. Thread safety: stateless; safe for concurrent use.
    /// </summary>
    public class TopicSlotService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly EntitlementService _Entitlements;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="entitlements">Entitlements.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TopicSlotService(DatabaseDriverBase database, EntitlementService entitlements)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        }

        /// <summary>
        /// The account's custom topic count against the current limit.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task<TopicSlotsResponse> DescribeAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            long? limit = await _Entitlements.TopicSlotLimitAsync(tenantId, userId, false, token).ConfigureAwait(false);
            List<Topic> ordered = await _Database.Topics.SlotOrderAsync(tenantId, userId, token).ConfigureAwait(false);
            return new TopicSlotsResponse
            {
                Limit = limit,
                Counted = ordered.Count,
                OverLimit = limit.HasValue ? Math.Max(0, ordered.Count - limit.Value) : 0,
                Customized = ordered.Any(t => t.SlotPriority.HasValue)
            };
        }

        /// <summary>
        /// One page of the account's custom topics in slot order. Position and WithinPlan are computed over the whole
        /// order, so a search or a later page keeps each topic's true slot. Search matches the topic name
        /// (case-insensitive); sorting is always slot order.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="query">Paging and search.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page of slots.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task<EnumerationResult<TopicSlotResponse>> EnumerateAsync(string tenantId, string userId, EnumerationQuery query, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            ArgumentNullException.ThrowIfNull(query);
            Stopwatch sw = Stopwatch.StartNew();
            long? limit = await _Entitlements.TopicSlotLimitAsync(tenantId, userId, false, token).ConfigureAwait(false);
            List<Topic> ordered = await _Database.Topics.SlotOrderAsync(tenantId, userId, token).ConfigureAwait(false);
            List<TopicSlotResponse> all = new List<TopicSlotResponse>();
            for (int i = 0; i < ordered.Count; i++)
            {
                Topic t = ordered[i];
                all.Add(new TopicSlotResponse
                {
                    TopicId = t.Id, Name = t.Name, Status = t.Status, Position = i + 1, Chosen = t.SlotPriority.HasValue,
                    WithinPlan = !limit.HasValue || i < limit.Value, LastRunUtc = t.LastRunUtc
                });
            }

            string? search = String.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            List<TopicSlotResponse> matched = search == null ? all : all.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            List<TopicSlotResponse> page = matched.Skip((int)Math.Min(query.Offset, Int32.MaxValue)).Take(query.PageSize).ToList();
            return EnumerationResult<TopicSlotResponse>.Create(page, query, matched.Count, sw.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// Save the keep order. Every ID must be a recurring, non-archived topic of the owner, listed once.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="keepTopicIds">Topic IDs, most important first. Empty clears the choice.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary after the change.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        /// <exception cref="ArgumentException">A topic is listed twice, unknown, archived, or one-time.</exception>
        public async Task<TopicSlotsResponse> SaveAsync(string tenantId, string userId, List<string> keepTopicIds, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            ArgumentNullException.ThrowIfNull(keepTopicIds);
            List<string> ids = keepTopicIds.Select(x => (x ?? String.Empty).Trim()).ToList();
            if (ids.Any(x => x.Length == 0)) throw new ArgumentException("keepTopicIds cannot contain blank values.");
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw new ArgumentException("keepTopicIds lists a topic more than once.");
            HashSet<string> owned = new HashSet<string>((await _Database.Topics.SlotOrderAsync(tenantId, userId, token).ConfigureAwait(false)).Select(t => t.Id), StringComparer.Ordinal);
            string? unknown = ids.FirstOrDefault(x => !owned.Contains(x));
            if (unknown != null) throw new ArgumentException("Topic " + unknown + " is not one of your recurring, non-archived topics.");
            await _Database.Topics.SetSlotPrioritiesAsync(tenantId, userId, ids, token).ConfigureAwait(false);
            return await DescribeAsync(tenantId, userId, token).ConfigureAwait(false);
        }
    }
}
