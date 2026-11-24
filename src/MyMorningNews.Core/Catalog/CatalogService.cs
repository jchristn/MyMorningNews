namespace MyMorningNews.Core.Catalog
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>Shared catalog access. Tenant boundaries are crossed only after explicit publication and subscription checks. Safe for concurrent callers.</summary>
    public class CatalogService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly EntitlementService _Entitlements;

        /// <summary>Create the catalog service. Dependencies must not be null.</summary>
        public CatalogService(DatabaseDriverBase database, EntitlementService entitlements)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        }

        /// <summary>Whether a plan restricts its catalog selection to administrator-designated free publications.</summary>
        public static bool FreeOnly(EffectivePlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            return plan.Features.TryGetValue("catalog.freeOnly", out string? value) && value == "true";
        }

        /// <summary>Apply current catalog eligibility and slot limits before returning subscriber data.</summary>
        public async Task<EffectivePlan> ReconcileAsync(string tenantId, string userId, CancellationToken token = default)
        {
            EffectivePlan plan = await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            await _Database.CatalogSubscriptions.ReconcileAsync(tenantId, userId, (long?)EntitlementService.Limit(plan, "systemSubscriptions.max"), FreeOnly(plan), token).ConfigureAwait(false);
            return plan;
        }

        /// <summary>Subscribe atomically. Throws CapReachedException when the account is full or ineligible, and ArgumentException for unavailable publications.</summary>
        public async Task<CatalogSubscription> SubscribeAsync(string tenantId, string userId, string publicationId, CatalogSubscriptionRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            CatalogPublication? publication = await _Database.CatalogPublications.ReadAsync(publicationId, token).ConfigureAwait(false);
            if (publication == null || !publication.Active || publication.Status != "Published") throw new ArgumentException("This catalog publication is unavailable.");
            EffectivePlan plan = await ReconcileAsync(tenantId, userId, token).ConfigureAwait(false);
            bool freeOnly = FreeOnly(plan);
            if (freeOnly && !publication.FreeEligible) throw new CapReachedException("catalog.freeOnly", "true", null, "Choose a publication included in your plan.");
            long? maximum = (long?)EntitlementService.Limit(plan, "systemSubscriptions.max");
            CatalogSubscription? subscription = await _Database.CatalogSubscriptions.TrySubscribeAsync(new CatalogSubscription
            {
                TenantId = tenantId, UserId = userId, PublicationId = publicationId,
                EmailEnabled = request.EmailEnabled, PushEnabled = request.PushEnabled
            }, maximum, freeOnly, token).ConfigureAwait(false);
            return subscription ?? throw new CapReachedException("systemSubscriptions.max", maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unlimited", null,
                "Your subscription slots are full, or this publication is no longer available. Unsubscribe from another publication to make room.");
        }

        /// <summary>Resolve a readable publication for one subscriber. Retired issues remain readable; retracted issues and ineligible subscriptions do not.</summary>
        public async Task<CatalogPublication?> ReadableAsync(string tenantId, string userId, string publicationId, CancellationToken token = default)
        {
            CatalogPublication? publication = await _Database.CatalogPublications.ReadAsync(publicationId, token).ConfigureAwait(false);
            if (publication == null || !publication.Active || (publication.Status != "Published" && publication.Status != "Retired")) return null;
            EffectivePlan plan = await ReconcileAsync(tenantId, userId, token).ConfigureAwait(false);
            if (FreeOnly(plan) && !publication.FreeEligible) return null;
            CatalogSubscription? subscription = (await _Database.CatalogSubscriptions.EnumerateAsync(new CatalogSubscriptionFilter
            { TenantId = tenantId, UserId = userId, PublicationId = publicationId, Active = true, Suspended = false, PageSize = 1 }, token).ConfigureAwait(false)).Objects.FirstOrDefault();
            return subscription == null ? null : publication;
        }

        /// <summary>Allowlisted issue content. Dry runs, other topics, and unfinished content are never projected.</summary>
        public async Task<CatalogIssueResponse?> IssueAsync(CatalogPublication publication, string issueId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(publication);
            Briefing? briefing = await _Database.Briefings.ReadAsync(publication.OwnerTenantId, issueId, token).ConfigureAwait(false);
            if (briefing == null || briefing.TopicId != publication.TopicId || briefing.Status != BriefingStatusEnum.Ready || briefing.IsDryRun || !briefing.Active) return null;
            MediaAsset? media = await AudioAsync(publication, issueId, token).ConfigureAwait(false);
            return new CatalogIssueResponse
            {
                Id = briefing.Id, PublicationId = publication.Id, Title = briefing.Title, Markdown = briefing.Markdown,
                CreatedUtc = briefing.CreatedUtc, AudioSeconds = media?.DurationSeconds,
                AudioPath = media == null ? null : "/v1.0/api/catalog/" + publication.Id + "/issues/" + briefing.Id + "/audio"
            };
        }

        /// <summary>Newest ready canonical audio, generated once and shared without charging subscribers.</summary>
        public async Task<MediaAsset?> AudioAsync(CatalogPublication publication, string issueId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(publication);
            return (await _Database.MediaAssets.EnumerateAsync(new MediaAssetFilter
            { TenantId = publication.OwnerTenantId, BriefingId = issueId, Status = MediaStatusEnum.Ready, Active = true, PageSize = 1 }, token).ConfigureAwait(false)).Objects.FirstOrDefault();
        }
    }
}
