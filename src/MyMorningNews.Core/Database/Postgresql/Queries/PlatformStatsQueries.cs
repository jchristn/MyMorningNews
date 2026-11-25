namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the useractivitydays table and the platform statistics behind the daily administrator summary.
    /// </summary>
    public static class PlatformStatsQueries
    {
        /// <summary>
        /// Activity table name.
        /// </summary>
        public const string Table = "useractivitydays";

        /// <summary>
        /// Schema statements. Idempotent. One row per user per UTC day with first and last activity.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS useractivitydays (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    day DATE NOT NULL,
    firstseenutc TIMESTAMPTZ NOT NULL,
    lastseenutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_useractivitydays_userid_day ON useractivitydays (userid, day)",
            @"CREATE INDEX IF NOT EXISTS ix_useractivitydays_day ON useractivitydays (day)",
            @"CREATE INDEX IF NOT EXISTS ix_useractivitydays_tenantid ON useractivitydays (tenantid)"
        };

        /// <summary>
        /// Record activity. $1 id, $2 tenant, $3 user, $4 day, $5 time.
        /// </summary>
        public const string Touch = "INSERT INTO useractivitydays (id, tenantid, userid, day, firstseenutc, lastseenutc) VALUES ($1, $2, $3, $4, $5, $5) " +
            "ON CONFLICT (userid, day) DO UPDATE SET lastseenutc = GREATEST(useractivitydays.lastseenutc, EXCLUDED.lastseenutc)";

        /// <summary>
        /// Delete activity days before $1.
        /// </summary>
        public const string Prune = "DELETE FROM useractivitydays WHERE day < $1";

        /// <summary>
        /// One row of platform counts. $1 period start, $2 period end, $3 seven days before the end, $4 thirty days
        /// before the end. Column order matches PlatformStatsMethods.MapDaily.
        /// </summary>
        public const string Daily = @"SELECT
 (SELECT COUNT(*) FROM users WHERE active = TRUE AND deletionrequestedutc IS NULL),
 (SELECT COUNT(*) FROM users WHERE createdutc >= $1 AND createdutc < $2),
 (SELECT COUNT(*) FROM users WHERE selfsignup = TRUE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COUNT(*) FROM users WHERE emailverifiedutc >= $1 AND emailverifiedutc < $2),
 (SELECT COUNT(*) FROM users WHERE deletionrequestedutc >= $1 AND deletionrequestedutc < $2),
 (SELECT COUNT(DISTINCT userid) FROM useractivitydays WHERE day >= $1::date AND day < $2::date),
 (SELECT COUNT(DISTINCT userid) FROM useractivitydays WHERE day >= $3::date AND day < $2::date),
 (SELECT COUNT(DISTINCT userid) FROM useractivitydays WHERE day >= $4::date AND day < $2::date),
 (SELECT COUNT(*) FROM users WHERE lastloginutc >= $1 AND lastloginutc < $2),
 (SELECT COUNT(*) FROM topics WHERE active = TRUE AND status = 'Active' AND isonetime = FALSE),
 (SELECT COUNT(*) FROM topics WHERE createdutc >= $1 AND createdutc < $2),
 (SELECT COUNT(*) FROM runs WHERE isdryrun = FALSE AND status IN ('Completed', 'PartiallySucceeded') AND completedutc >= $1 AND completedutc < $2),
 (SELECT COUNT(*) FROM runs WHERE isdryrun = FALSE AND status = 'Failed' AND lastupdateutc >= $1 AND lastupdateutc < $2),
 (SELECT COUNT(*) FROM runs WHERE isdryrun = FALSE AND status = 'Skipped' AND lastupdateutc >= $1 AND lastupdateutc < $2),
 (SELECT COALESCE(SUM(newfindingcount), 0) FROM runs WHERE isdryrun = FALSE AND completedutc >= $1 AND completedutc < $2),
 (SELECT COALESCE(AVG(durationms), 0) FROM runs WHERE isdryrun = FALSE AND status IN ('Completed', 'PartiallySucceeded') AND completedutc >= $1 AND completedutc < $2),
 (SELECT COUNT(*) FROM briefings WHERE isdryrun = FALSE AND status = 'Ready' AND composedutc >= $1 AND composedutc < $2),
 (SELECT COUNT(*) FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COUNT(*) FROM llmcalls WHERE isdryrun = FALSE AND success = FALSE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COALESCE(SUM(inputtokens), 0) FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COALESCE(SUM(outputtokens), 0) FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COALESCE(SUM(estimatedcostusd), 0) FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COALESCE(SUM(durationms), 0) FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COALESCE(SUM(estimatedcostusd), 0) FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $4 AND createdutc < $2),
 (SELECT COUNT(*) FROM mediaassets WHERE status = 'Ready' AND completedutc >= $1 AND completedutc < $2),
 (SELECT COALESCE(SUM(durationseconds), 0) FROM mediaassets WHERE status = 'Ready' AND completedutc >= $1 AND completedutc < $2),
 (SELECT COUNT(*) FROM emailmessages WHERE status = 'Sent' AND sentutc >= $1 AND sentutc < $2),
 (SELECT COUNT(*) FROM emailmessages WHERE status = 'Failed' AND failedutc >= $1 AND failedutc < $2),
 (SELECT COUNT(*) FROM devices WHERE active = TRUE),
 (SELECT COUNT(*) FROM devices WHERE createdutc >= $1 AND createdutc < $2),
 (SELECT COUNT(*) FROM billingsubscriptions WHERE active = TRUE AND endsutc > $2 AND status IN ('Active', 'Grace', 'Canceled')),
 (SELECT COUNT(*) FROM billingsubscriptions WHERE active = TRUE AND createdutc >= $1 AND createdutc < $2),
 (SELECT COUNT(*) FROM catalogsubscriptions WHERE active = TRUE),
 (SELECT COUNT(*) FROM topicreviews WHERE promptedutc >= $1 AND promptedutc < $2),
 (SELECT COUNT(*) FROM topicreviews WHERE status = 'Kept' AND resolvedutc >= $1 AND resolvedutc < $2),
 (SELECT COUNT(*) FROM topicreviews WHERE status IN ('Deleted', 'AutoDeleted') AND resolvedutc >= $1 AND resolvedutc < $2)";

        /// <summary>
        /// Top five provider and model pairs by cost in the period. $1 period start, $2 period end.
        /// </summary>
        public const string TopModels = @"SELECT COALESCE(provider, ''), COALESCE(model, ''), COUNT(*), COALESCE(SUM(inputtokens + outputtokens), 0), COALESCE(SUM(estimatedcostusd), 0)
FROM llmcalls WHERE isdryrun = FALSE AND createdutc >= $1 AND createdutc < $2
GROUP BY 1, 2 ORDER BY 5 DESC, 3 DESC LIMIT 5";
    }
}
