namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the promocodes table.
    /// </summary>
    public static class PromoCodeQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "promocodes";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, code, description, planid, durationdays, maxredemptions, redemptioncount, peruserlimit, startsutc, expiresutc, enabled, newaccountsonly, batchid, createdbyuserid, active, createdutc, lastupdateutc, benefitkind, billingplankey, discountpercent, discountmonths, appleofferid, googleofferid";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS promocodes (
    id VARCHAR(64) PRIMARY KEY,
    code VARCHAR(40) NOT NULL,
    description TEXT,
    planid VARCHAR(64) NOT NULL,
    durationdays INTEGER,
    maxredemptions INTEGER,
    redemptioncount INTEGER NOT NULL,
    peruserlimit INTEGER NOT NULL,
    startsutc TIMESTAMPTZ,
    expiresutc TIMESTAMPTZ,
    enabled BOOLEAN NOT NULL,
    newaccountsonly BOOLEAN NOT NULL,
    batchid VARCHAR(64),
    createdbyuserid VARCHAR(64),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_promocodes_code ON promocodes (code)",
            @"CREATE INDEX IF NOT EXISTS ix_promocodes_createdutc ON promocodes (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO promocodes (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23)";

        /// <summary>
        /// Update statement. $1 is id, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE promocodes SET code = $2, description = $3, planid = $4, durationdays = $5, maxredemptions = $6, redemptioncount = GREATEST(redemptioncount, $7), peruserlimit = $8, startsutc = $9, expiresutc = $10, enabled = $11, newaccountsonly = $12, batchid = $13, createdbyuserid = $14, active = $15, lastupdateutc = $16, benefitkind = $17, billingplankey = $18, discountpercent = $19, discountmonths = $20, appleofferid = $21, googleofferid = $22 WHERE id = $1";

        /// <summary>
        /// Select by identifier.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM promocodes WHERE id = $1";

        /// <summary>
        /// Delete by identifier.
        /// </summary>
        public const string Delete = "DELETE FROM promocodes WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM promocodes WHERE id = $1 LIMIT 1";
    }
}
