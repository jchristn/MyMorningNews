namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing schedules.
    /// </summary>
    public class ScheduleListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only schedules owned by this kind of record. Null by default.
        /// </summary>
        public ScheduleOwnerTypeEnum? OwnerType { get; set; } = null;

        /// <summary>
        /// Only schedules of this topic or edition. Null by default.
        /// </summary>
        public string? OwnerId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ScheduleListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "ownerType", OwnerType);
            QueryFormat.Add(query, "ownerId", OwnerId);
        }
    }
}
