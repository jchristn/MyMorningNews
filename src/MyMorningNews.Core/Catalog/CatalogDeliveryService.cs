namespace MyMorningNews.Core.Catalog
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Email;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;

    /// <summary>Durable fanout for canonical issues. Email and in-app inserts are idempotent; push retries carry a stable delivery ID.</summary>
    public class CatalogDeliveryService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private readonly EmailService _Email;
        private readonly PushService _Push;
        private readonly CatalogService _Catalog;

        /// <summary>Bind live settings, delivery services and the database. Safe for concurrent workers.</summary>
        public CatalogDeliveryService(DatabaseDriverBase database, ServerSettings settings, EmailService email, PushService push)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Email = email ?? throw new ArgumentNullException(nameof(email));
            _Push = push ?? throw new ArgumentNullException(nameof(push));
            _Catalog = new CatalogService(database, new EntitlementService(database, settings));
        }

        /// <summary>Discover new issues and process up to 100 leased subscriber/channel deliveries. Returns the number claimed.</summary>
        public async Task<int> ProcessAsync(CancellationToken token = default)
        {
            foreach (CatalogDelivery candidate in await _Database.CatalogDeliveries.CandidatesAsync(token).ConfigureAwait(false))
                await _Database.CatalogDeliveries.EnqueueAsync(candidate, token).ConfigureAwait(false);
            int processed = 0;
            for (int i = 0; i < 100; i++)
            {
                CatalogDelivery? item = await _Database.CatalogDeliveries.ClaimAsync(token).ConfigureAwait(false);
                if (item == null) break;
                processed++;
                try
                {
                    if (!await IsAllowedAsync(_Database, _Settings, _Email.Preferences, item, token).ConfigureAwait(false)) item.Status = "Skipped";
                    else
                    {
                        CatalogPublication publication = (await _Catalog.ReadableAsync(item.TenantId, item.UserId, item.PublicationId, token).ConfigureAwait(false))!;
                        CatalogIssueResponse issue = (await _Catalog.IssueAsync(publication, item.BriefingId, token).ConfigureAwait(false))!;
                        User user = (await _Database.Users.ReadAsync(item.TenantId, item.UserId, token).ConfigureAwait(false))!;
                        if (item.Channel == "Email")
                        {
                            string key = "catalog:" + item.Id;
                            await _Email.QueueNoticeAsync(user, new EmailNotice
                            {
                                Key = key, Template = EmailTemplateEnum.BriefingPublished, Category = EmailCategoryEnum.BriefingEmails,
                                BriefingId = issue.Id, Title = issue.Title, Subtitle = publication.Title,
                                Paragraphs = new List<string> { "A new issue of your system briefing is ready. Read it or listen through your subscription." },
                                Button = new EmailLink("Open briefing", _Settings.Rest.AppBaseUrl.TrimEnd('/') + "/dashboard/catalog/" + Uri.EscapeDataString(publication.Id) + "/issues/" + Uri.EscapeDataString(issue.Id)),
                                Note = "You receive this because you subscribed to this system briefing. Manage delivery in the system catalog or notification preferences."
                            }, token).ConfigureAwait(false);
                            EmailMessage? mail = await _Database.EmailMessages.ReadByKeyAsync(user.TenantId, key + ":" + EmailService.Normalize(user.Email), token).ConfigureAwait(false);
                            if (mail == null || (mail.Status == EmailStatusEnum.Sending && String.IsNullOrEmpty(mail.RenderedHtml))) throw new InvalidOperationException("Catalog email rendering has not completed.");
                        }
                        else if (item.Channel == "InApp")
                        {
                            await _Database.CatalogDeliveries.NotifyAsync(item, new Notification { TenantId = user.TenantId, UserId = user.Id,
                                Title = issue.Title.Length > 256 ? issue.Title.Substring(0, 256) : issue.Title, Body = "A new issue of " + publication.Title + " is ready." }, token).ConfigureAwait(false);
                            continue;
                        }
                        else
                        {
                            PushResult push = await _Push.SendToUserAsync(user.TenantId, user.Id, new PushMessage { Title = issue.Title, Body = "Your system briefing is ready.", ChannelId = "briefings",
                                Data = new Dictionary<string, string> { { "kind", "catalog.ready" }, { "catalogPublicationId", publication.Id }, { "briefingId", issue.Id }, { "deliveryId", item.Id } } }, token).ConfigureAwait(false);
                            if (push.Failed > push.Removed) throw new InvalidOperationException("Catalog push delivery will retry.");
                        }
                        item.Status = "Completed";
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    item.Status = "Pending";
                    item.RetryUtc = DateTime.UtcNow.AddSeconds(Math.Min(3600, 15 * Math.Pow(2, Math.Min(8, item.Attempts))));
                }
                await _Database.CatalogDeliveries.FinishAsync(item, token).ConfigureAwait(false);
            }
            return processed;
        }

        /// <summary>Recheck current subscription, content, account, channel preferences and email verification before every delivery.</summary>
        public static async Task<bool> IsAllowedAsync(DatabaseDriverBase database, ServerSettings settings, NotificationPreferenceService preferences, CatalogDelivery item, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(item);
            User? user = await database.Users.ReadAsync(item.TenantId, item.UserId, token).ConfigureAwait(false);
            if (user == null || !user.Active) return false;
            CatalogService catalog = new CatalogService(database, new EntitlementService(database, settings));
            CatalogPublication? publication = await catalog.ReadableAsync(item.TenantId, item.UserId, item.PublicationId, token).ConfigureAwait(false);
            if (publication == null || publication.Status != "Published") return false;
            Briefing? briefing = await database.Briefings.ReadAsync(publication.OwnerTenantId, item.BriefingId, token).ConfigureAwait(false);
            if (briefing == null || !briefing.Active || briefing.IsDryRun || briefing.Status != BriefingStatusEnum.Ready || briefing.TopicId != publication.TopicId) return false;
            CatalogSubscription? subscription = await database.CatalogSubscriptions.ReadAsync(item.TenantId, item.SubscriptionId, token).ConfigureAwait(false);
            if (subscription == null || !subscription.Active || subscription.Suspended || subscription.UserId != item.UserId || subscription.PublicationId != item.PublicationId) return false;
            if ((briefing.ComposedUtc ?? briefing.CreatedUtc) < subscription.CreatedUtc) return false;
            if (item.Channel == "Email" && (!subscription.EmailEnabled || String.IsNullOrWhiteSpace(user.Email) || (user.SelfSignup && settings.Hosting.RequireEmailVerification && !user.EmailVerifiedUtc.HasValue))) return false;
            if (item.Channel == "Push" && !subscription.PushEnabled) return false;
            if (!Enum.TryParse(item.Channel, out NotificationChannelEnum channel)) return false;
            return await preferences.IsEnabledAsync(user, NotificationKindEnum.BriefingReady, channel, token).ConfigureAwait(false);
        }
    }
}
