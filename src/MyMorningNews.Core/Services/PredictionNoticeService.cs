namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Tells owners when their predictions reach their horizon and are due for review (owner decision 2026-11-20: one
    /// push per prediction). Each prediction is noticed once (<see cref="Prediction.DueNotifiedUtc"/>). When one user has
    /// more than <see cref="GroupAbove"/> predictions due in the same pass, they get one combined push instead. Payloads
    /// carry prediction IDs, never the statement. Thread safe (stateless beyond the database).
    /// </summary>
    public class PredictionNoticeService
    {
        #region Public-Members

        /// <summary>
        /// Above this many due predictions for one user in one pass, send one combined push. Default 3, minimum 1,
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
        private readonly PushService _Push;
        private readonly LoggingModule _Logging;
        private int _GroupAbove = 3;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the service.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="push">Push service.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PredictionNoticeService(DatabaseDriverBase database, PushService push, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Push = push ?? throw new ArgumentNullException(nameof(push));
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
                        await _Push.SendToUserAsync(first.TenantId, first.UserId, new PushMessage
                        {
                            Title = items.Count + " predictions are due",
                            Body = "Check whether they came true.",
                            ChannelId = "predictions",
                            Data = new Dictionary<string, string> { { "kind", "prediction.due" }, { "count", items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) } }
                        }, token).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (Prediction p in items)
                        {
                            await _Push.SendToUserAsync(p.TenantId, p.UserId, new PushMessage
                            {
                                Title = "A prediction is due",
                                Body = "Check whether it came true.",
                                ChannelId = "predictions",
                                Data = new Dictionary<string, string> { { "kind", "prediction.due" }, { "predictionId", p.Id } }
                            }, token).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Logging.Warn("[PredictionNotice] push failed for user " + first.UserId + ": " + ex.Message);
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
