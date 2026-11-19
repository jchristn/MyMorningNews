namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Write or edit a learned preference. Null fields are unchanged on update.
    /// </summary>
    public class PreferenceUpsertRequest
    {
        /// <summary>
        /// The preference as an instruction.
        /// </summary>
        public string? Statement { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public PreferenceKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Topic; empty for all topics.
        /// </summary>
        public string? TopicId { get; set; } = null;
    }
}
