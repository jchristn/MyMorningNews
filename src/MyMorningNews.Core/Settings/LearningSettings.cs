namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Learning from feedback (plan section 18).
    /// </summary>
    public class LearningSettings
    {
        /// <summary>
        /// Learn preferences from feedback. Default true.
        /// </summary>
        [Setting(Description = "Learn preferences from feedback.", EnvironmentVariable = "LEARNING_ENABLED")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// New feedback items on a topic that trigger a learning pass. Default 5, minimum 1, maximum 100.
        /// </summary>
        [Setting(Description = "New feedback items that trigger a learning pass.", Minimum = 1, Maximum = 100)]
        public int FeedbackPerPass
        {
            get { return _FeedbackPerPass; }
            set { _FeedbackPerPass = Math.Clamp(value, 1, 100); }
        }

        /// <summary>
        /// Make distilled preferences active without asking. Default false.
        /// </summary>
        [Setting(Description = "Apply distilled preferences without asking.")]
        public bool AutoApply { get; set; } = false;

        /// <summary>
        /// Minutes between learning passes. Default 10, minimum 1, maximum 1440.
        /// </summary>
        [Setting(Description = "Minutes between learning passes.", Minimum = 1, Maximum = 1440)]
        public int IntervalMinutes
        {
            get { return _IntervalMinutes; }
            set { _IntervalMinutes = Math.Clamp(value, 1, 1440); }
        }

        /// <summary>
        /// Rated findings whose embeddings shape the feedback score (nearest neighbors). Default 20, minimum 1, maximum 200.
        /// </summary>
        [Setting(Description = "Nearest rated findings used for the feedback score.", Minimum = 1, Maximum = 200)]
        public int FeedbackNeighbors
        {
            get { return _FeedbackNeighbors; }
            set { _FeedbackNeighbors = Math.Clamp(value, 1, 200); }
        }

        private int _FeedbackPerPass = 5;
        private int _IntervalMinutes = 10;
        private int _FeedbackNeighbors = 20;
    }
}
