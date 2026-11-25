namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Keep-or-delete reviews of recurring topics that stop producing new findings. A topic whose runs found nothing
    /// new for <see cref="TopicLifecycleSettings.StaleDays"/> gets a review and its owner a prompt (email, push, and
    /// the in-app banner). Keep holds off the next prompt for <see cref="TopicLifecycleSettings.KeepDays"/>; Delete
    /// deletes the topic now; new findings close the review by themselves; no answer by the deadline
    /// (<see cref="TopicLifecycleSettings.GraceDays"/>) deletes the topic, after a reminder.
    /// Thread safety: stateless apart from injected services; one pass at a time is expected (the worker), and the
    /// unique pending-review index keeps overlapping passes from prompting twice.
    /// </summary>
    public class TopicLifecycleService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly NotificationService _Notifications;
        private readonly BriefingService _Briefings;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private const string _Header = "[TopicLifecycleService] ";
        private int _BatchSize = 500;

        /// <summary>
        /// Rows handled per step in one pass. Default 500, minimum 1, maximum 5000.
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
        /// <param name="notifications">Notifications.</param>
        /// <param name="briefings">Briefings (deletes topic files).</param>
        /// <param name="settings">Settings.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TopicLifecycleService(DatabaseDriverBase database, NotificationService notifications, BriefingService briefings, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _Briefings = briefings ?? throw new ArgumentNullException(nameof(briefings));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// One pass: close reviews whose topic found something new, delete topics past their deadline, send reminders,
        /// and prompt for newly quiet topics. Does nothing when the feature is off. Each item is handled on its own, so
        /// one failure is logged and the pass goes on.
        /// </summary>
        /// <param name="nowUtc">Current time, UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Counts.</returns>
        public async Task<TopicLifecycleResult> RunOnceAsync(DateTime nowUtc, CancellationToken token = default)
        {
            TopicLifecycleResult result = new TopicLifecycleResult();
            TopicLifecycleSettings s = _Settings.TopicLifecycle;
            if (!s.Enabled) return result;

            foreach (TopicReview review in await _Database.TopicReviews.ListPendingAsync(BatchSize, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await AdvanceAsync(review, nowUtc, result, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Logging.Warn(_Header + "review " + review.Id + " failed: " + ex.Message);
                }
            }

            List<StaleTopicCandidate> candidates = await _Database.TopicReviews.ListStaleCandidatesAsync(nowUtc.AddDays(-s.StaleDays), nowUtc.AddDays(-s.KeepDays), BatchSize, token).ConfigureAwait(false);
            foreach (StaleTopicCandidate candidate in candidates)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (await PromptAsync(candidate, nowUtc, token).ConfigureAwait(false)) result.Prompted++;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Logging.Warn(_Header + "prompt for topic " + candidate.TopicId + " failed: " + ex.Message);
                }
            }

            if (result.Prompted + result.Reminded + result.Recovered + result.Deleted > 0)
                _Logging.Info(_Header + "prompted " + result.Prompted + ", reminded " + result.Reminded + ", recovered " + result.Recovered + ", deleted " + result.Deleted);
            return result;
        }

        /// <summary>
        /// A user's pending reviews, earliest deadline first.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="filter">Paging (tenant, user, and status are forced).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page of reviews with topic names.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task<EnumerationResult<TopicReviewResponse>> ListPendingAsync(string tenantId, string userId, TopicReviewFilter filter, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            ArgumentNullException.ThrowIfNull(filter);
            filter.TenantId = tenantId;
            filter.UserId = userId;
            filter.Status = TopicReviewStatusEnum.Pending;
            filter.SortBy ??= "deleteAfterUtc";
            EnumerationResult<TopicReview> page = await _Database.TopicReviews.EnumerateAsync(filter, token).ConfigureAwait(false);
            List<TopicReviewResponse> items = new List<TopicReviewResponse>();
            foreach (TopicReview r in page.Objects)
            {
                Topic? topic = await _Database.Topics.ReadAsync(r.TenantId, r.TopicId, token).ConfigureAwait(false);
                items.Add(TopicReviewResponse.From(r, topic?.Name, _Settings.TopicLifecycle.StaleDays));
            }

            return EnumerationResult<TopicReviewResponse>.Create(items, filter, page.TotalRecords, page.TotalMs);
        }

        /// <summary>
        /// Read one of a user's reviews.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User; the review must belong to this user.</param>
        /// <param name="reviewId">Review.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Review with the topic name, or null when not found or not the user's.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task<TopicReviewResponse?> ReadAsync(string tenantId, string userId, string reviewId, CancellationToken token = default)
        {
            TopicReview? review = await OwnedAsync(tenantId, userId, reviewId, token).ConfigureAwait(false);
            if (review == null) return null;
            Topic? topic = await _Database.Topics.ReadAsync(review.TenantId, review.TopicId, token).ConfigureAwait(false);
            return TopicReviewResponse.From(review, topic?.Name, _Settings.TopicLifecycle.StaleDays);
        }

        /// <summary>
        /// Keep the topic. Idempotent for a review already kept.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User; the review must belong to this user.</param>
        /// <param name="reviewId">Review.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The review, or null when not found or not the user's.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        /// <exception cref="InvalidOperationException">The review was already closed another way.</exception>
        public async Task<TopicReviewResponse?> KeepAsync(string tenantId, string userId, string reviewId, CancellationToken token = default)
        {
            TopicReview? review = await OwnedAsync(tenantId, userId, reviewId, token).ConfigureAwait(false);
            if (review == null) return null;
            if (review.Status == TopicReviewStatusEnum.Pending)
            {
                review.Status = TopicReviewStatusEnum.Kept;
                review.ResolvedUtc = DateTime.UtcNow;
                await _Database.TopicReviews.UpdateAsync(review, token).ConfigureAwait(false);
            }
            else if (review.Status != TopicReviewStatusEnum.Kept && review.Status != TopicReviewStatusEnum.Recovered)
            {
                throw new InvalidOperationException("This topic was already deleted.");
            }

            Topic? topic = await _Database.Topics.ReadAsync(review.TenantId, review.TopicId, token).ConfigureAwait(false);
            return TopicReviewResponse.From(review, topic?.Name, _Settings.TopicLifecycle.StaleDays);
        }

        /// <summary>
        /// Delete the topic from the prompt, with its briefings, findings, and files.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User; the review must belong to this user.</param>
        /// <param name="reviewId">Review.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when deleted (or already gone), false when the review was not found or not the user's.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        /// <exception cref="InvalidOperationException">The owner already chose Keep, or the topic found something new.</exception>
        public async Task<bool> DeleteTopicAsync(string tenantId, string userId, string reviewId, CancellationToken token = default)
        {
            TopicReview? review = await OwnedAsync(tenantId, userId, reviewId, token).ConfigureAwait(false);
            if (review == null) return false;
            if (review.Status == TopicReviewStatusEnum.Deleted || review.Status == TopicReviewStatusEnum.AutoDeleted) return true;
            if (review.Status != TopicReviewStatusEnum.Pending) throw new InvalidOperationException("This review is closed. Delete the topic from its page instead.");
            await RemoveTopicAsync(review, TopicReviewStatusEnum.Deleted, token).ConfigureAwait(false);
            return true;
        }

        private async Task<TopicReview?> OwnedAsync(string tenantId, string userId, string reviewId, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            if (String.IsNullOrWhiteSpace(reviewId)) throw new ArgumentNullException(nameof(reviewId));
            TopicReview? review = await _Database.TopicReviews.ReadAsync(tenantId, reviewId, token).ConfigureAwait(false);
            return review != null && review.UserId == userId ? review : null;
        }

        // Moves one pending review along: recovered, deleted, reminded, or left alone.
        private async Task AdvanceAsync(TopicReview review, DateTime nowUtc, TopicLifecycleResult result, CancellationToken token)
        {
            Topic? topic = await _Database.Topics.ReadAsync(review.TenantId, review.TopicId, token).ConfigureAwait(false);
            if (topic == null)
            {
                // Deleted another way (topic page, account deletion); close the review quietly.
                review.Status = TopicReviewStatusEnum.Deleted;
                review.ResolvedUtc = nowUtc;
                await _Database.TopicReviews.UpdateAsync(review, token).ConfigureAwait(false);
                return;
            }

            DateTime? lastNew = await _Database.TopicReviews.ReadLastNewFindingAsync(review.TenantId, review.TopicId, token).ConfigureAwait(false);
            if (lastNew.HasValue && lastNew.Value > review.PromptedUtc)
            {
                review.Status = TopicReviewStatusEnum.Recovered;
                review.LastNewFindingUtc = lastNew;
                review.ResolvedUtc = nowUtc;
                await _Database.TopicReviews.UpdateAsync(review, token).ConfigureAwait(false);
                result.Recovered++;
                return;
            }

            if (review.DeleteAfterUtc <= nowUtc)
            {
                await NotifyAsync(review, topic.Name, TopicReviewNoticeEnum.Deleted, token).ConfigureAwait(false);
                await RemoveTopicAsync(review, TopicReviewStatusEnum.AutoDeleted, token).ConfigureAwait(false);
                result.Deleted++;
                return;
            }

            int reminderDays = _Settings.TopicLifecycle.ReminderDays;
            if (reminderDays > 0 && !review.ReminderSentUtc.HasValue && review.DeleteAfterUtc.AddDays(-reminderDays) <= nowUtc)
            {
                review.ReminderSentUtc = nowUtc;
                await _Database.TopicReviews.UpdateAsync(review, token).ConfigureAwait(false);
                await NotifyAsync(review, topic.Name, TopicReviewNoticeEnum.Reminder, token).ConfigureAwait(false);
                result.Reminded++;
            }
        }

        private async Task<bool> PromptAsync(StaleTopicCandidate candidate, DateTime nowUtc, CancellationToken token)
        {
            Topic? topic = await _Database.Topics.ReadAsync(candidate.TenantId, candidate.TopicId, token).ConfigureAwait(false);
            if (topic == null) return false;
            TopicReview review = new TopicReview
            {
                TenantId = candidate.TenantId,
                UserId = candidate.UserId,
                TopicId = candidate.TopicId,
                LastNewFindingUtc = candidate.LastNewFindingUtc,
                PromptedUtc = nowUtc,
                DeleteAfterUtc = nowUtc.AddDays(_Settings.TopicLifecycle.GraceDays)
            };
            try
            {
                review = await _Database.TopicReviews.CreateAsync(review, token).ConfigureAwait(false);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
            {
                return false;
            }

            await NotifyAsync(review, topic.Name, TopicReviewNoticeEnum.Prompt, token).ConfigureAwait(false);
            return true;
        }

        private async Task NotifyAsync(TopicReview review, string topicName, TopicReviewNoticeEnum notice, CancellationToken token)
        {
            await _Notifications.NotifyAsync(review.TenantId, review.UserId, NotificationKindEnum.TopicStale, new NotificationContext
            {
                Review = review,
                ReviewNotice = notice,
                TopicName = topicName,
                StaleDays = _Settings.TopicLifecycle.StaleDays
            }, token).ConfigureAwait(false);
        }

        private async Task RemoveTopicAsync(TopicReview review, TopicReviewStatusEnum status, CancellationToken token)
        {
            await _Briefings.DeleteTopicFilesAsync(review.TenantId, review.TopicId, token).ConfigureAwait(false);
            await _Database.Topics.DeleteCascadeAsync(review.TenantId, review.TopicId, token).ConfigureAwait(false);
            review.Status = status;
            review.ResolvedUtc = DateTime.UtcNow;
            await _Database.TopicReviews.UpdateAsync(review, token).ConfigureAwait(false);
        }
    }
}
