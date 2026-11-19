namespace MyMorningNews.Core.Pipeline
{
    using System;

    /// <summary>
    /// What a stage consumed and produced, recorded on its runstages row.
    /// </summary>
    public class StageOutcome
    {
        /// <summary>
        /// Items in.
        /// </summary>
        public int ItemsIn { get; set; } = 0;

        /// <summary>
        /// Items out.
        /// </summary>
        public int ItemsOut { get; set; } = 0;

        /// <summary>
        /// Optional note (for example a fallback that was used).
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// Create an outcome.
        /// </summary>
        /// <param name="itemsIn">Items in.</param>
        /// <param name="itemsOut">Items out.</param>
        /// <param name="note">Note.</param>
        /// <returns>Outcome.</returns>
        public static StageOutcome Of(int itemsIn, int itemsOut, string? note = null)
        {
            return new StageOutcome { ItemsIn = Math.Max(0, itemsIn), ItemsOut = Math.Max(0, itemsOut), Note = note };
        }
    }
}
