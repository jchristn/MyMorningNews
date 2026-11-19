namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for VoiceProfile records. Null properties are not applied.
    /// </summary>
    public class VoiceProfileFilter : EnumerationFilter
    {
        /// <summary>
        /// Match Provider exactly when set.
        /// </summary>
        public TtsProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Match IsDefault exactly when set.
        /// </summary>
        public bool? IsDefault { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
