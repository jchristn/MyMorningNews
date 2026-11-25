namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topicreviews table.
    /// </summary>
    public static class TopicReviewQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topicreviews";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, topicid, status, lastnewfindingutc, promptedutc, deleteafterutc, remindersentutc, resolvedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent. The topic reference has no foreign key on purpose: a review
        /// outlives the topic it deleted, so the daily administrator summary can count automatic deletions.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topicreviews (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    status VARCHAR(16) NOT NULL,
    lastnewfindingutc TIMESTAMPTZ,
    promptedutc TIMESTAMPTZ NOT NULL,
    deleteafterutc TIMESTAMPTZ NOT NULL,
    remindersentutc TIMESTAMPTZ,
    resolvedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topicreviews_tenantid_userid ON topicreviews (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicreviews_topicid ON topicreviews (topicid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_topicreviews_pending_topic ON topicreviews (topicid) WHERE status = 'Pending'",
            @"CREATE INDEX IF NOT EXISTS ix_topicreviews_status_deleteafterutc ON topicreviews (status, deleteafterutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO topicreviews (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns.
        /// </summary>
        public const string Update = "UPDATE topicreviews SET status = $3, lastnewfindingutc = $4, deleteafterutc = $5, remindersentutc = $6, resolvedutc = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM topicreviews WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Pending reviews across tenants, earliest deadline first. $1 is the row limit.
        /// </summary>
        public const string SelectPending = "SELECT " + Columns + " FROM topicreviews WHERE status = 'Pending' AND active = TRUE ORDER BY deleteafterutc ASC, id ASC LIMIT $1";

        /// <summary>
        /// Recurring active topics that qualify for a review: created before the window start, at least one completed
        /// real run since the window start, no real run with new findings since the window start, not a catalog
        /// topic, no pending review, and no review answered Kept or closed as Recovered since the snooze cutoff.
        /// $1 is the window start, $2 the snooze cutoff, $3 the row limit. Columns: topic id, tenant id, user id, and
        /// the completion time of the last run with new findings (null when none).
        /// </summary>
        public const string SelectStaleCandidates = @"SELECT t.id, t.tenantid, t.userid,
    (SELECT MAX(r2.completedutc) FROM runs r2 WHERE r2.tenantid = t.tenantid AND r2.topicid = t.id AND r2.isdryrun = FALSE AND r2.newfindingcount > 0) AS lastnew
FROM topics t
WHERE t.active = TRUE AND t.status = 'Active' AND t.isonetime = FALSE AND t.createdutc <= $1
  AND EXISTS (SELECT 1 FROM runs r WHERE r.tenantid = t.tenantid AND r.topicid = t.id AND r.isdryrun = FALSE AND r.status = 'Completed' AND r.completedutc >= $1)
  AND NOT EXISTS (SELECT 1 FROM runs r WHERE r.tenantid = t.tenantid AND r.topicid = t.id AND r.isdryrun = FALSE AND r.newfindingcount > 0 AND r.completedutc >= $1)
  AND NOT EXISTS (SELECT 1 FROM catalogpublications c WHERE c.topicid = t.id)
  AND NOT EXISTS (SELECT 1 FROM topicreviews v WHERE v.topicid = t.id AND (v.status = 'Pending' OR (v.status IN ('Kept', 'Recovered') AND v.resolvedutc >= $2)))
ORDER BY t.createdutc ASC, t.id ASC LIMIT $3";

        /// <summary>
        /// Completion time of the newest real run with new findings for a topic. $1 is the tenant, $2 the topic.
        /// </summary>
        public const string SelectLastNewFinding = "SELECT MAX(completedutc) FROM runs WHERE tenantid = $1 AND topicid = $2 AND isdryrun = FALSE AND newfindingcount > 0";
    }
}
