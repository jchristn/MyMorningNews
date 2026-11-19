namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Supported database providers. PostgreSQL is the only provider (see the plan, section 4, C1).
    /// </summary>
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// PostgreSQL with pgvector.
        /// </summary>
        Postgresql
    }
}
