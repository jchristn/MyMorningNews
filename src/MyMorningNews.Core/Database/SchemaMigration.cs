namespace MyMorningNews.Core.Database
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A versioned, ordered set of idempotent schema statements tracked in schema_migrations.
    /// </summary>
    public class SchemaMigration
    {
        #region Public-Members

        /// <summary>
        /// Migration version. Minimum 1. Versions apply in ascending order.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Ordered statements.
        /// </summary>
        public IReadOnlyList<string> Statements { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="version">Version, 1 or greater.</param>
        /// <param name="description">Description.</param>
        /// <param name="statements">Ordered statements.</param>
        /// <exception cref="ArgumentOutOfRangeException">version is less than 1.</exception>
        /// <exception cref="ArgumentNullException">description or statements is null.</exception>
        public SchemaMigration(int version, string description, IReadOnlyList<string> statements)
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "Migration versions start at 1.");
            if (String.IsNullOrWhiteSpace(description)) throw new ArgumentNullException(nameof(description));
            ArgumentNullException.ThrowIfNull(statements);
            Version = version;
            Description = description;
            Statements = statements;
        }

        #endregion
    }
}
