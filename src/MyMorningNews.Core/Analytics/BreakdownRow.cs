namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A rate for one group.
    /// </summary>
    public class BreakdownRow
    {
        /// <summary>
        /// Group (topic, tier, domain, dimension, model, or day).
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Display label, when the key is an ID.
        /// </summary>
        public string? Label { get; set; } = null;

        /// <summary>
        /// Items in the group.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Items counted as positive (relevant, accurate, verified).
        /// </summary>
        public int Positive { get; set; } = 0;

        /// <summary>
        /// Positive over count, or null when empty.
        /// </summary>
        public double? Rate { get; set; } = null;

        /// <summary>
        /// Another measure, such as mean Brier.
        /// </summary>
        public double? Value { get; set; } = null;
    }
}
