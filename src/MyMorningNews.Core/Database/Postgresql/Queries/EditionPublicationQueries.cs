namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the editionpublications table.
    /// </summary>
    public static class EditionPublicationQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "editionpublications";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, editionid, publicationid, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS editionpublications (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    editionid VARCHAR(64) NOT NULL REFERENCES editions(id) ON DELETE CASCADE,
    publicationid VARCHAR(64) NOT NULL REFERENCES catalogpublications(id) ON DELETE CASCADE,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_editionpublications_edition_publication ON editionpublications (editionid, publicationid)",
            @"CREATE INDEX IF NOT EXISTS ix_editionpublications_tenant_edition ON editionpublications (tenantid, editionid, sortorder)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO editionpublications (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

        /// <summary>
        /// Delete every link of one edition.
        /// </summary>
        public const string DeleteByEdition = "DELETE FROM editionpublications WHERE tenantid = $1 AND editionid = $2";
    }
}
