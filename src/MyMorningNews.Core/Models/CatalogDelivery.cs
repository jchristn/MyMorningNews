namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Helpers;

    /// <summary>Durable per-subscriber, per-issue, per-channel delivery. Mutable records are owned by one database lease.</summary>
    public class CatalogDelivery
    {
        /// <summary>Delivery ID with cld_ prefix.</summary>
        public string Id { get; set; } = IdGenerator.GenerateCatalogDeliveryId();
        /// <summary>Subscriber tenant.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>Subscriber account.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Private subscription identity.</summary>
        public string SubscriptionId { get; set; } = String.Empty;
        /// <summary>Public catalog publication.</summary>
        public string PublicationId { get; set; } = String.Empty;
        /// <summary>Canonical issue, accessible only through publication checks.</summary>
        public string BriefingId { get; set; } = String.Empty;
        /// <summary>Email, Push, or InApp.</summary>
        public string Channel { get; set; } = String.Empty;
        /// <summary>Pending, Processing, Completed, or Skipped.</summary>
        public string Status { get; set; } = "Pending";
        /// <summary>Monotonic lease attempt used to reject stale workers.</summary>
        public int Attempts { get; set; } = 0;
        /// <summary>Next retry or lease expiry in UTC.</summary>
        public DateTime RetryUtc { get; set; } = DateTime.UtcNow;
    }
}
