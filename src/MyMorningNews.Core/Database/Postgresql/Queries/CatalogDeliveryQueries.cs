namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>Catalog fanout queue schema and explicit columns.</summary>
    public static class CatalogDeliveryQueries
    {
        /// <summary>Stable mapping order.</summary>
        public const string Columns = "id,tenantid,userid,subscriptionid,publicationid,briefingid,channel,status,attempts,retryutc";
        /// <summary>One delivery per subscription, issue and channel; dependent account/content removal cascades.</summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS catalogdeliveries (
id VARCHAR(64) PRIMARY KEY,
tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
subscriptionid VARCHAR(64) NOT NULL REFERENCES catalogsubscriptions(id) ON DELETE CASCADE,
publicationid VARCHAR(64) NOT NULL REFERENCES catalogpublications(id) ON DELETE CASCADE,
briefingid VARCHAR(64) NOT NULL REFERENCES briefings(id) ON DELETE CASCADE,
channel VARCHAR(16) NOT NULL CHECK (channel IN ('Email','Push','InApp')),
status VARCHAR(16) NOT NULL DEFAULT 'Pending', attempts INTEGER NOT NULL DEFAULT 0,
retryutc TIMESTAMPTZ NOT NULL DEFAULT now(),
UNIQUE(subscriptionid,briefingid,channel))",
            "CREATE INDEX IF NOT EXISTS ix_catalogdeliveries_due ON catalogdeliveries(retryutc) WHERE status IN ('Pending','Processing')"
        };
    }
}
