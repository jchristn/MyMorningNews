namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A built-in plan definition.
    /// </summary>
    public class PlanSeed
    {
        /// <summary>
        /// Key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Order.
        /// </summary>
        public int SortOrder { get; }

        /// <summary>
        /// Entitlement values.
        /// </summary>
        public IReadOnlyDictionary<string, string> Features { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="sortOrder">Order.</param>
        /// <param name="features">Entitlement values.</param>
        public PlanSeed(string key, string name, string description, int sortOrder, IReadOnlyDictionary<string, string> features)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? String.Empty;
            SortOrder = sortOrder;
            Features = features ?? new Dictionary<string, string>();
        }
    }
}
