namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// Topic constraint kind.
    /// </summary>
    public enum ConstraintKindEnum
    {
        /// <summary>
        /// Include keyword.
        /// </summary>
        IncludeKeyword,

        /// <summary>
        /// Exclude keyword.
        /// </summary>
        ExcludeKeyword,

        /// <summary>
        /// Always include entity.
        /// </summary>
        IncludeEntity,

        /// <summary>
        /// Ignore entity.
        /// </summary>
        ExcludeEntity,

        /// <summary>
        /// Preferred domain.
        /// </summary>
        PreferredDomain,

        /// <summary>
        /// Blocked domain.
        /// </summary>
        BlockedDomain,

        /// <summary>
        /// Question to answer over time.
        /// </summary>
        TrackedQuestion,

        /// <summary>
        /// RSS or Atom feed.
        /// </summary>
        FeedUrl,

        /// <summary>
        /// Seed page.
        /// </summary>
        SeedUrl
    }
}
