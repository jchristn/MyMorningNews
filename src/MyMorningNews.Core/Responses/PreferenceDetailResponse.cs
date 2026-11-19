namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A learned preference with the feedback behind it.
    /// </summary>
    public class PreferenceDetailResponse
    {
        /// <summary>
        /// Preference.
        /// </summary>
        public LearnedPreference Preference { get; set; } = new LearnedPreference();

        /// <summary>
        /// Supporting feedback.
        /// </summary>
        public List<Feedback> Support { get; set; } = new List<Feedback>();
    }
}
