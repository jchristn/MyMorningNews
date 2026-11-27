namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using SyslogLogging;

    /// <summary>
    /// Tells an owner, once, when the account has more recurring custom topics than its plan allows (plan section 8.4).
    /// The limit can drop for many reasons (a downgrade, an expired or canceled subscription, an administrator plan
    /// assignment or limit override, a billing provider change, a promotion ending), so nothing hooks those paths:
    /// each check compares the account's current topic slots (<see cref="TopicSlotService.DescribeAsync"/>, the same
    /// numbers the keep-order screen shows) with the notice recorded for it. Going over records a notice and sends a
    /// <see cref="NotificationKindEnum.TopicsOverLimit"/> notification; staying over sends nothing more; coming back
    /// within the limit removes the record, so going over again notifies again. The record is kept even when the owner
    /// turned the kind off, so turning it back on never sends a late notice for an old change.
    /// Thread safety: stateless apart from injected services; the atomic insert keeps overlapping checks from
    /// notifying twice.
    /// </summary>
    public class TopicSlotNoticeService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly TopicSlotService _Slots;
        private readonly NotificationService _Notifications;
        private readonly LoggingModule _Logging;
        private const string _Header = "[TopicSlotNoticeService] ";
        private int _BatchSize = 500;

        /// <summary>
        /// Accounts read per page in one pass. Default 500, minimum 1, maximum 5000.
        /// </summary>
        public int BatchSize
        {
            get { return _BatchSize; }
            set { _BatchSize = Math.Clamp(value, 1, 5000); }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="entitlements">Entitlements (the effective plan and its topic limit).</param>
        /// <param name="notifications">Notifications.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TopicSlotNoticeService(DatabaseDriverBase database, EntitlementService entitlements, NotificationService notifications, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            ArgumentNullException.ThrowIfNull(entitlements);
            _Slots = new TopicSlotService(database, entitlements);
            _Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Check one account and notify its owner when it just went over its custom topic limit.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>What the check did.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        public async Task<TopicSlotNoticeOutcomeEnum> CheckAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            TopicSlotsResponse slots = await _Slots.DescribeAsync(tenantId, userId, token).ConfigureAwait(false);
            TopicSlotNotice? recorded = await _Database.TopicSlotNotices.ReadAsync(tenantId, userId, token).ConfigureAwait(false);
            if (slots.OverLimit < 1)
            {
                if (recorded == null) return TopicSlotNoticeOutcomeEnum.WithinLimit;
                await _Database.TopicSlotNotices.DeleteAsync(tenantId, userId, token).ConfigureAwait(false);
                return TopicSlotNoticeOutcomeEnum.Cleared;
            }

            if (recorded != null) return TopicSlotNoticeOutcomeEnum.AlreadyNotified;
            TopicSlotNotice notice = new TopicSlotNotice
            {
                TenantId = tenantId,
                UserId = userId,
                OverLimit = slots.OverLimit,
                TopicLimit = slots.Limit ?? 0,
                NotifiedUtc = DateTime.UtcNow
            };
            if (!await _Database.TopicSlotNotices.TryCreateAsync(notice, token).ConfigureAwait(false)) return TopicSlotNoticeOutcomeEnum.AlreadyNotified;
            await _Notifications.NotifyAsync(tenantId, userId, NotificationKindEnum.TopicsOverLimit, new NotificationContext
            {
                TopicSlots = slots,
                OverLimitSinceUtc = notice.NotifiedUtc
            }, token).ConfigureAwait(false);
            return TopicSlotNoticeOutcomeEnum.Notified;
        }

        /// <summary>
        /// One pass over every account that owns a recurring topic or has a recorded notice. Each account is handled on
        /// its own, so one failure is logged and the pass goes on.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Owners notified in this pass.</returns>
        public async Task<int> RunOnceAsync(CancellationToken token = default)
        {
            int notified = 0;
            string? after = null;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                List<TopicSlotNotice> page = await _Database.TopicSlotNotices.ListAccountsAsync(after, BatchSize, token).ConfigureAwait(false);
                if (!page.Any()) break;
                foreach (TopicSlotNotice account in page)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (await CheckAsync(account.TenantId, account.UserId, token).ConfigureAwait(false) == TopicSlotNoticeOutcomeEnum.Notified) notified++;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _Logging.Warn(_Header + "topic limit check for user " + account.UserId + " failed: " + ex.Message);
                    }
                }

                if (page.Count < BatchSize) break;
                after = page[page.Count - 1].UserId;
            }

            return notified;
        }
    }
}
