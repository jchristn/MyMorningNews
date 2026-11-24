namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;

    /// <summary>Durable catalog delivery queue. Implementations are safe for concurrent workers.</summary>
    public interface ICatalogDeliveryMethods
    {
        /// <summary>Find up to 300 new subscriber/channel deliveries, excluding historical issues predating subscription.</summary>
        Task<List<CatalogDelivery>> CandidatesAsync(CancellationToken token = default);
        /// <summary>Insert once for subscription, issue and channel.</summary>
        Task EnqueueAsync(CatalogDelivery item, CancellationToken token = default);
        /// <summary>Read an internal delivery identity. Never expose this unscoped lookup through an API.</summary>
        Task<CatalogDelivery?> ReadAsync(string id, CancellationToken token = default);
        /// <summary>Lease one due delivery for five minutes, recovering crashed workers.</summary>
        Task<CatalogDelivery?> ClaimAsync(CancellationToken token = default);
        /// <summary>Complete, skip or retry the current lease only. False means a newer worker owns it.</summary>
        Task<bool> FinishAsync(CatalogDelivery item, CancellationToken token = default);
        /// <summary>Insert one subscriber notification and complete its claim in a single transaction.</summary>
        Task NotifyAsync(CatalogDelivery item, Notification notification, CancellationToken token = default);
    }
}
