namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Mixed audio.
    /// </summary>
    public class MixResult
    {
        /// <summary>
        /// Audio bytes.
        /// </summary>
        public byte[] Audio { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Media type.
        /// </summary>
        public string ContentType { get; set; } = "audio/mpeg";

        /// <summary>
        /// File extension.
        /// </summary>
        public string Extension { get; set; } = "mp3";

        /// <summary>
        /// Total duration.
        /// </summary>
        public double DurationSeconds { get; set; } = 0;

        /// <summary>
        /// Duration of each input segment, in order.
        /// </summary>
        public List<double> SegmentDurations { get; set; } = new List<double>();
    }
}
