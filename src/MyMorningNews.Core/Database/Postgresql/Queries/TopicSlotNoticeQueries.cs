namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topicslotnotices table: one row per account that was told it has more custom topics than its plan
    /// allows.
    /// </summary>
    public static class TopicSlotNoticeQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topicslotnotices";

        /// <summary>
        /// Schema statements. Idempotent. The row goes away with the user.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topicslotnotices (
    userid VARCHAR(64) PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    overlimit BIGINT NOT NULL CHECK (overlimit >= 0),
    topiclimit BIGINT NOT NULL CHECK (topiclimit >= 0),
    notifiedutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topicslotnotices_tenantid ON topicslotnotices (tenantid)"
        };

        /// <summary>
        /// Read one. $1 tenant, $2 user.
        /// </summary>
        public const string Select = "SELECT tenantid, userid, overlimit, topiclimit, notifiedutc FROM topicslotnotices WHERE tenantid = $1 AND userid = $2";

        /// <summary>
        /// Record unless present. $1 user, $2 tenant, $3 over limit, $4 limit, $5 notified.
        /// </summary>
        public const string Insert = "INSERT INTO topicslotnotices (userid, tenantid, overlimit, topiclimit, notifiedutc) VALUES ($1, $2, $3, $4, $5) ON CONFLICT (userid) DO NOTHING";

        /// <summary>
        /// Remove. $1 tenant, $2 user.
        /// </summary>
        public const string Delete = "DELETE FROM topicslotnotices WHERE tenantid = $1 AND userid = $2";

        /// <summary>
        /// Accounts to check, after user $1 (empty for the first page), at most $2. Column order matches
        /// TopicSlotNoticeMethods.Map.
        /// </summary>
        public const string Accounts = @"SELECT a.tenantid, a.userid, COALESCE(n.overlimit, 0), COALESCE(n.topiclimit, 0), n.notifiedutc
FROM (SELECT tenantid, userid FROM topics WHERE NOT isonetime AND status <> 'Archived' GROUP BY tenantid, userid
      UNION SELECT tenantid, userid FROM topicslotnotices) a
JOIN users u ON u.id = a.userid AND u.tenantid = a.tenantid AND u.active = TRUE
LEFT JOIN topicslotnotices n ON n.userid = a.userid
WHERE a.userid > $1
ORDER BY a.userid
LIMIT $2";
    }
}
