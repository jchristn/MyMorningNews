namespace MyMorningNews.Core.Briefings
{
    using System;

    /// <summary>
    /// Audio options from a template front matter audio block.
    /// </summary>
    public class TemplateAudioOptions
    {
        /// <summary>
        /// Produce audio. Default false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// narrator or two-host. Default narrator.
        /// </summary>
        public string Format { get; set; } = "narrator";

        /// <summary>
        /// Target length in minutes (1 to 60). Default 8.
        /// </summary>
        public int TargetMinutes { get; set; } = 8;

        /// <summary>
        /// Spoken intro, or null.
        /// </summary>
        public string? Intro { get; set; } = null;
    }
}
