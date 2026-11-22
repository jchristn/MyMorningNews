namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Tells owners when their predictions reach their horizon and are due for review (owner decision 2026-11-20: one
    /// notice per prediction), through <see cref="NotificationService"/> on the channels the owner turned on. Each
    /// prediction is noticed once (<see cref="Prediction.DueNotifiedUtc"/>). When one user has more than
    /// <see cref="GroupAbove"/> predictions due in the same pass, they get one combined notice instead. Push payloads
    /// carry prediction IDs, never the statement. Thread safe (stateless beyond the database).
    /// </summary>
    public class PredictionNoticeService
    {
        #region Public-Members

        /// <summary>
        /// Above this many due predictions for one user in one pass, send one combined notice. Default 3, minimum 1,
        /// maximum 50.
        /// </summary>
        public int GroupAbove
        {
            get { return _GroupAbove; }
            set { _GroupAbove = Math.Clamp(value, 1, 50); }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly NotificationService _Notifications;
        private readonly LoggingModule _Logging;
        private int _GroupAbove = 3;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the service.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="notifications">Notifications.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PredictionNoticeService(DatabaseDriverBase database, NotificationService notifications, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send notices for predictions that have come due, and mark them noticed.
        /// </summary>
        /// <param name="nowUtc">Current time.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Predictions marked noticed.</returns>
        public async Task<int> NotifyDueAsync(DateTime nowUtc, CancellationToken token = default)
        {
            List<Prediction> due = await _Database.Predictions.ListDueForNoticeAsync(nowUtc, token).ConfigureAwait(false);
            int marked = 0;
            foreach (IGrouping<string, Prediction> group in due.GroupBy(p => p.TenantId + "|" + p.UserId, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                List<Prediction> items = group.ToList();
                Prediction first = items[0];
                try
                {
                    if (items.Count > _GroupAbove)
                    {
                        await _Notifications.NotifyAsync(first.TenantId, first.UserId, NotificationKindEnum.PredictionDue, new NotificationContext { Prediction = first, PredictionCount = items.Count }, token).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (Prediction p in items)
                            await _Notifications.NotifyAsync(p.TenantId, p.UserId, NotificationKindEnum.PredictionDue, new NotificationContext { Prediction = p, PredictionCount = 1 }, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Logging.Warn("[PredictionNotice] notice failed for user " + first.UserId + ": " + ex.Message);
                }

                foreach (Prediction p in items)
                {
                    p.DueNotifiedUtc = nowUtc;
                    await _Database.Predictions.UpdateAsync(p, token).ConfigureAwait(false);
                    marked++;
                }
            }

            return marked;
        }

        #endregion
    }
}
