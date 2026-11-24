namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>Append-only successful generation ledger. There are no edit or media-delete refund operations.</summary>
    public static class AudioUsageQueries
    {
        /// <summary>Ledger and indexes. Account deletion may purge its ledger; deleting audio cannot.</summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS audiousageentries (
                id VARCHAR(64) PRIMARY KEY,
                tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                reservationid VARCHAR(64) NOT NULL UNIQUE REFERENCES audioreservations(id) ON DELETE CASCADE,
                operationid VARCHAR(64) NOT NULL,
                periodstartutc TIMESTAMPTZ NOT NULL,
                milliseconds BIGINT NOT NULL CHECK (milliseconds > 0),
                createdutc TIMESTAMPTZ NOT NULL DEFAULT now())",
            "CREATE INDEX IF NOT EXISTS ix_audiousageentries_account_period ON audiousageentries (tenantid, userid, periodstartutc)"
        };
    }
}
