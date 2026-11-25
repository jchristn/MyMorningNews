namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// The one place that answers "does this user want this kind of notification on this channel". A stored row wins;
    /// without one the default applies. Email choices made before the matrix existed (an account-wide unsubscribe from
    /// run, briefing, or failure emails) are read until the user saves that cell, and saving the cell replaces them, so
    /// the old email preferences and the matrix never disagree. Thread safety: safe for concurrent use (stateless beyond
    /// the database).
    /// </summary>
    public class NotificationPreferenceService
    {
        #region Public-Members

        /// <summary>
        /// Every kind, in display order.
        /// </summary>
        public static IReadOnlyList<NotificationKindEnum> Kinds { get; } = new List<NotificationKindEnum>
        {
            NotificationKindEnum.RunResults,
            NotificationKindEnum.BriefingReady,
            NotificationKindEnum.AudioReady,
            NotificationKindEnum.RunFailed,
            NotificationKindEnum.CapReached,
            NotificationKindEnum.PredictionDue,
            NotificationKindEnum.TopicStale
        };

        /// <summary>
        /// Every channel, in display order.
        /// </summary>
        public static IReadOnlyList<NotificationChannelEnum> Channels { get; } = new List<NotificationChannelEnum>
        {
            NotificationChannelEnum.Email,
            NotificationChannelEnum.Push,
            NotificationChannelEnum.InApp
        };

        /// <summary>
        /// The kinds that were pushed to every registered device before push became a per-kind choice. Owners of devices
        /// registered back then keep them.
        /// </summary>
        public static IReadOnlyList<NotificationKindEnum> LegacyPushKinds { get; } = new List<NotificationKindEnum>
        {
            NotificationKindEnum.BriefingReady,
            NotificationKindEnum.RunFailed,
            NotificationKindEnum.CapReached,
            NotificationKindEnum.PredictionDue
        };

        /// <summary>
        /// The kinds turned on for push when an account registers its first device without having made any push choice:
        /// new findings and ready briefings (a new edition), failures, plan limits, due predictions, and stale topic
        /// prompts. Audio ready stays off because the briefing push already announces the edition.
        /// </summary>
        public static IReadOnlyList<NotificationKindEnum> DefaultPushKinds { get; } = new List<NotificationKindEnum>
        {
            NotificationKindEnum.RunResults,
            NotificationKindEnum.BriefingReady,
            NotificationKindEnum.RunFailed,
            NotificationKindEnum.CapReached,
            NotificationKindEnum.PredictionDue,
            NotificationKindEnum.TopicStale
        };

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">database is null.</exception>
        public NotificationPreferenceService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The setting used when the user never chose. Email is on for run results, ready briefings, failed runs, and
        /// plan limits, and off for audio and predictions. Push and in-app are off for every kind.
        /// </summary>
        /// <param name="kind">Kind.</param>
        /// <param name="channel">Channel.</param>
        /// <returns>True when on by default.</returns>
        public static bool Default(NotificationKindEnum kind, NotificationChannelEnum channel)
        {
            if (channel != NotificationChannelEnum.Email) return false;
            return kind == NotificationKindEnum.RunResults || kind == NotificationKindEnum.BriefingReady || kind == NotificationKindEnum.RunFailed || kind == NotificationKindEnum.CapReached || kind == NotificationKindEnum.TopicStale;
        }

        /// <summary>
        /// The kind an email category belongs to.
        /// </summary>
        /// <param name="category">Email category.</param>
        /// <returns>Kind, or null for <see cref="EmailCategoryEnum.All"/>.</returns>
        public static NotificationKindEnum? KindFor(EmailCategoryEnum category)
        {
            switch (category)
            {
                case EmailCategoryEnum.RunEmails: return NotificationKindEnum.RunResults;
                case EmailCategoryEnum.BriefingEmails: return NotificationKindEnum.BriefingReady;
                case EmailCategoryEnum.FailureEmails: return NotificationKindEnum.RunFailed;
                case EmailCategoryEnum.AudioEmails: return NotificationKindEnum.AudioReady;
                case EmailCategoryEnum.PredictionEmails: return NotificationKindEnum.PredictionDue;
                case EmailCategoryEnum.TopicReviewEmails: return NotificationKindEnum.TopicStale;
                default: return null;
            }
        }

        /// <summary>
        /// The email category of a kind, used by unsubscribe links.
        /// </summary>
        /// <param name="kind">Kind.</param>
        /// <returns>Category, or null for plan limit notices, which have no unsubscribe category.</returns>
        public static EmailCategoryEnum? CategoryFor(NotificationKindEnum kind)
        {
            switch (kind)
            {
                case NotificationKindEnum.RunResults: return EmailCategoryEnum.RunEmails;
                case NotificationKindEnum.BriefingReady: return EmailCategoryEnum.BriefingEmails;
                case NotificationKindEnum.RunFailed: return EmailCategoryEnum.FailureEmails;
                case NotificationKindEnum.AudioReady: return EmailCategoryEnum.AudioEmails;
                case NotificationKindEnum.PredictionDue: return EmailCategoryEnum.PredictionEmails;
                case NotificationKindEnum.TopicStale: return EmailCategoryEnum.TopicReviewEmails;
                default: return null;
            }
        }

        /// <summary>
        /// Whether a user wants a kind on a channel.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="channel">Channel.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when wanted.</returns>
        /// <exception cref="ArgumentNullException">user is null.</exception>
        public async Task<bool> IsEnabledAsync(User user, NotificationKindEnum kind, NotificationChannelEnum channel, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            NotificationPreference? row = await ReadRowAsync(user, kind, channel, token).ConfigureAwait(false);
            if (row != null) return row.Enabled;
            if (channel == NotificationChannelEnum.Email && (await EarlierOptOutsAsync(user, token).ConfigureAwait(false)).Contains(kind)) return false;
            return Default(kind, channel);
        }

        /// <summary>
        /// Every kind on every channel for a user, in kind order and then channel order.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Cells.</returns>
        /// <exception cref="ArgumentNullException">user is null.</exception>
        public async Task<List<NotificationPreferenceCell>> ReadAllAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            List<NotificationPreference> rows = (await _Database.NotificationPreferences.EnumerateAsync(new NotificationPreferenceFilter { TenantId = user.TenantId, UserId = user.Id, PageSize = 200 }, token).ConfigureAwait(false)).Objects;
            HashSet<NotificationKindEnum> optedOut = await EarlierOptOutsAsync(user, token).ConfigureAwait(false);
            List<NotificationPreferenceCell> cells = new List<NotificationPreferenceCell>();
            foreach (NotificationKindEnum kind in Kinds)
            {
                foreach (NotificationChannelEnum channel in Channels)
                {
                    NotificationPreference? row = rows.FirstOrDefault(r => r.Kind == kind && r.Channel == channel);
                    bool enabled = row != null ? row.Enabled : (channel == NotificationChannelEnum.Email && optedOut.Contains(kind) ? false : Default(kind, channel));
                    cells.Add(new NotificationPreferenceCell { Kind = kind, Channel = channel, Enabled = enabled });
                }
            }

            return cells;
        }

        /// <summary>
        /// Save one cell. Saving an email cell also removes the account-wide unsubscribe it replaces.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="channel">Channel.</param>
        /// <param name="enabled">Wanted or not.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">user is null.</exception>
        public async Task SetAsync(User user, NotificationKindEnum kind, NotificationChannelEnum channel, bool enabled, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            NotificationPreference? row = await ReadRowAsync(user, kind, channel, token).ConfigureAwait(false);
            if (row == null)
            {
                try
                {
                    await _Database.NotificationPreferences.CreateAsync(new NotificationPreference { TenantId = user.TenantId, UserId = user.Id, Kind = kind, Channel = channel, Enabled = enabled }, token).ConfigureAwait(false);
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
                {
                    // Another request saved the same cell first; update that row.
                    row = await ReadRowAsync(user, kind, channel, token).ConfigureAwait(false);
                }
            }

            if (row != null && row.Enabled != enabled)
            {
                row.Enabled = enabled;
                await _Database.NotificationPreferences.UpdateAsync(row, token).ConfigureAwait(false);
            }

            EmailCategoryEnum? category = CategoryFor(kind);
            string address = EmailService.Normalize(user.Email);
            if (channel != NotificationChannelEnum.Email || !category.HasValue || address.Length == 0) return;
            List<EmailSuppression> earlier = (await _Database.EmailSuppressions.EnumerateAsync(new EmailSuppressionFilter { TenantId = user.TenantId, Recipient = address, Category = category.Value, PageSize = 500 }, token).ConfigureAwait(false)).Objects;
            foreach (EmailSuppression s in earlier.Where(s => s.TopicId == null))
                await _Database.EmailSuppressions.DeleteAsync(s.TenantId, s.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Turn on the default push kinds (<see cref="DefaultPushKinds"/>, a superset of what older app builds delivered,
        /// <see cref="LegacyPushKinds"/>) for a user who has never made a push choice. Does nothing when the user has any
        /// push preference row, so a choice the user made is never undone.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Cells turned on.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        public async Task<int> KeepEarlierPushAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            long chosen = await _Database.NotificationPreferences.CountAsync(new NotificationPreferenceFilter { TenantId = tenantId, UserId = userId, Channel = NotificationChannelEnum.Push }, token).ConfigureAwait(false);
            if (chosen > 0) return 0;
            int created = 0;
            foreach (NotificationKindEnum kind in DefaultPushKinds)
            {
                try
                {
                    await _Database.NotificationPreferences.CreateAsync(new NotificationPreference { TenantId = tenantId, UserId = userId, Kind = kind, Channel = NotificationChannelEnum.Push, Enabled = true }, token).ConfigureAwait(false);
                    created++;
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
                {
                    // Saved by another request in the meantime; that choice stands.
                }
            }

            return created;
        }

        /// <summary>
        /// One-time upgrade step, safe to run at every start: owners of devices registered before push became a
        /// per-kind choice keep the kinds they already received.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Users upgraded.</returns>
        public async Task<int> KeepEarlierPushForDevicesAsync(CancellationToken token = default)
        {
            List<Device> devices = await _Database.NotificationPreferences.ListDevicesWithoutPushChoicesAsync(token).ConfigureAwait(false);
            int users = 0;
            foreach (Device d in devices.GroupBy(d => d.UserId, StringComparer.Ordinal).Select(g => g.First()))
            {
                token.ThrowIfCancellationRequested();
                if (await KeepEarlierPushAsync(d.TenantId, d.UserId, token).ConfigureAwait(false) > 0) users++;
            }

            return users;
        }

        #endregion

        #region Private-Methods

        private async Task<NotificationPreference?> ReadRowAsync(User user, NotificationKindEnum kind, NotificationChannelEnum channel, CancellationToken token)
        {
            return (await _Database.NotificationPreferences.EnumerateAsync(new NotificationPreferenceFilter { TenantId = user.TenantId, UserId = user.Id, Kind = kind, Channel = channel, PageSize = 1 }, token).ConfigureAwait(false)).Objects.FirstOrDefault();
        }

        private async Task<HashSet<NotificationKindEnum>> EarlierOptOutsAsync(User user, CancellationToken token)
        {
            HashSet<NotificationKindEnum> kinds = new HashSet<NotificationKindEnum>();
            string address = EmailService.Normalize(user.Email);
            if (address.Length == 0) return kinds;
            List<EmailSuppression> rows = (await _Database.EmailSuppressions.EnumerateAsync(new EmailSuppressionFilter { TenantId = user.TenantId, Recipient = address, PageSize = 500 }, token).ConfigureAwait(false)).Objects;
            foreach (EmailSuppression s in rows.Where(s => s.TopicId == null))
            {
                NotificationKindEnum? kind = KindFor(s.Category);
                if (kind.HasValue) kinds.Add(kind.Value);
            }

            return kinds;
        }

        #endregion
    }
}
