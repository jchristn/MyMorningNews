namespace MyMorningNews.Core.Email
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The actions an email link can carry, by item (plan section 23.2), with their labels.
    /// </summary>
    public static class EmailActions
    {
        /// <summary>
        /// Finding actions.
        /// </summary>
        public static readonly IReadOnlyList<string> Finding = new List<string> { "relevant", "somewhat", "not-relevant" };

        /// <summary>
        /// Actions on a new prediction.
        /// </summary>
        public static readonly IReadOnlyList<string> PredictionNew = new List<string> { "relevant", "not-relevant" };

        /// <summary>
        /// Actions on a prediction to grade.
        /// </summary>
        public static readonly IReadOnlyList<string> PredictionGrade = new List<string> { "accurate", "partially", "inaccurate", "too-early" };

        /// <summary>
        /// Suggestion actions.
        /// </summary>
        public static readonly IReadOnlyList<string> Suggestion = new List<string> { "useful", "not-useful", "done", "snooze" };

        /// <summary>
        /// Actions on a learned preference proposal.
        /// </summary>
        public static readonly IReadOnlyList<string> Preference = new List<string> { "apply", "dismiss" };

        /// <summary>
        /// Unsubscribe action.
        /// </summary>
        public const string Unsubscribe = "unsubscribe";

        private static readonly Dictionary<string, string> _Labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "relevant", "Relevant" },
            { "somewhat", "Somewhat" },
            { "not-relevant", "Not relevant" },
            { "accurate", "Accurate" },
            { "partially", "Partially" },
            { "inaccurate", "Inaccurate" },
            { "too-early", "Too early" },
            { "useful", "Useful" },
            { "not-useful", "Not useful" },
            { "done", "Done" },
            { "snooze", "Snooze a week" },
            { "unsubscribe", "Unsubscribe" },
            { "apply", "Apply" },
            { "dismiss", "Dismiss" }
        };

        /// <summary>
        /// Label for an action.
        /// </summary>
        /// <param name="action">Action.</param>
        /// <returns>Label.</returns>
        public static string Label(string? action)
        {
            return action != null && _Labels.TryGetValue(action, out string? l) ? l : (action ?? String.Empty);
        }
    }
}
