namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Models;
    using Npgsql;

    /// <summary>Database leases and idempotent fanout for canonical issues.</summary>
    public class CatalogDeliveryMethods : PostgresqlMethodsBase, ICatalogDeliveryMethods
    {
        /// <summary>Bind to the PostgreSQL driver.</summary>
        public CatalogDeliveryMethods(PostgresqlDatabaseDriver driver) : base(driver) { }

        /// <inheritdoc />
        public Task<List<CatalogDelivery>> CandidatesAsync(CancellationToken token = default)
        {
            return QueryAsync(@"SELECT s.tenantid,s.userid,s.id,p.id,b.id,c.channel
FROM catalogsubscriptions s JOIN catalogpublications p ON p.id=s.publicationid
JOIN briefings b ON b.tenantid=p.ownertenantid AND b.topicid=p.topicid
CROSS JOIN (VALUES ('Email'),('Push'),('InApp')) c(channel)
WHERE s.active AND NOT s.suspended AND p.active AND p.status='Published'
AND b.active AND b.status='Ready' AND NOT b.isdryrun
AND COALESCE(b.composedutc,b.createdutc) >= s.createdutc
AND COALESCE(b.composedutc,b.createdutc) >= COALESCE(p.publishedutc,p.createdutc)
AND NOT EXISTS (SELECT 1 FROM catalogdeliveries d WHERE d.subscriptionid=s.id AND d.briefingid=b.id AND d.channel=c.channel)
ORDER BY b.createdutc,s.id,c.channel LIMIT 300", r => new CatalogDelivery
            { TenantId = r.GetString(0), UserId = r.GetString(1), SubscriptionId = r.GetString(2), PublicationId = r.GetString(3), BriefingId = r.GetString(4), Channel = r.GetString(5) }, token);
        }

        /// <inheritdoc />
        public async Task EnqueueAsync(CatalogDelivery item, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(item);
            await ExecuteAsync("INSERT INTO catalogdeliveries (" + CatalogDeliveryQueries.Columns + ") VALUES ($1,$2,$3,$4,$5,$6,$7,'Pending',0,now()) ON CONFLICT (subscriptionid,briefingid,channel) DO NOTHING",
                token, item.Id, item.TenantId, item.UserId, item.SubscriptionId, item.PublicationId, item.BriefingId, item.Channel).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<CatalogDelivery?> ReadAsync(string id, CancellationToken token = default)
        {
            return (await QueryAsync("SELECT " + CatalogDeliveryQueries.Columns + " FROM catalogdeliveries WHERE id=$1", Map, token, id).ConfigureAwait(false)).FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<CatalogDelivery?> ClaimAsync(CancellationToken token = default)
        {
            return (await QueryAsync(@"UPDATE catalogdeliveries SET status='Processing', attempts=attempts+1,retryutc=now()+interval '5 minutes'
WHERE id=(SELECT id FROM catalogdeliveries WHERE status IN ('Pending','Processing') AND retryutc<=now() ORDER BY retryutc,id FOR UPDATE SKIP LOCKED LIMIT 1)
RETURNING " + CatalogDeliveryQueries.Columns, Map, token).ConfigureAwait(false)).FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> FinishAsync(CatalogDelivery item, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Status != "Completed" && item.Status != "Skipped" && item.Status != "Pending") throw new ArgumentException("Unsupported delivery result.", nameof(item));
            return await ExecuteAsync("UPDATE catalogdeliveries SET status=$3,retryutc=$4 WHERE id=$1 AND attempts=$2 AND status='Processing'", token,
                item.Id, item.Attempts, item.Status, DateTime.SpecifyKind(item.RetryUtc, DateTimeKind.Utc)).ConfigureAwait(false) == 1;
        }

        /// <inheritdoc />
        public async Task NotifyAsync(CatalogDelivery item, Notification notification, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(notification);
            await ExecuteAsync(@"WITH claimed AS (UPDATE catalogdeliveries SET status='Completed' WHERE id=$1 AND attempts=$2 AND status='Processing' AND channel='InApp' RETURNING tenantid,userid)
INSERT INTO notifications (id,tenantid,userid,kind,title,body,briefingid,catalogpublicationid,active,createdutc,lastupdateutc)
SELECT $3,tenantid,userid,'BriefingReady',$4,$5,$6,$7,TRUE,now(),now() FROM claimed", token,
                item.Id, item.Attempts, notification.Id, notification.Title, notification.Body, item.BriefingId, item.PublicationId).ConfigureAwait(false);
        }

        private static CatalogDelivery Map(NpgsqlDataReader reader)
        {
            return new CatalogDelivery { Id = reader.GetString(0), TenantId = reader.GetString(1), UserId = reader.GetString(2), SubscriptionId = reader.GetString(3),
                PublicationId = reader.GetString(4), BriefingId = reader.GetString(5), Channel = reader.GetString(6), Status = reader.GetString(7), Attempts = reader.GetInt32(8), RetryUtc = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc) };
        }
    }
}
