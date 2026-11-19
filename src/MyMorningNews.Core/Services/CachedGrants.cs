namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Security;

    /// <summary>
    /// Cached effective grants tagged with the authorization version they were computed at.
    /// </summary>
    public class CachedGrants
    {
        /// <summary>
        /// Authorization version.
        /// </summary>
        public long Version { get; }

        /// <summary>
        /// Grants.
        /// </summary>
        public List<EffectiveGrant> Grants { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="version">Version.</param>
        /// <param name="grants">Grants.</param>
        /// <exception cref="ArgumentNullException">grants is null.</exception>
        public CachedGrants(long version, List<EffectiveGrant> grants)
        {
            ArgumentNullException.ThrowIfNull(grants);
            Version = version;
            Grants = grants;
        }
    }
}
