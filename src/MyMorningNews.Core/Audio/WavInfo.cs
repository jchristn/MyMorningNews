namespace MyMorningNews.Core.Audio
{
    using System;

    /// <summary>
    /// Parsed WAV header.
    /// </summary>
    public class WavInfo
    {
        /// <summary>
        /// Samples per second.
        /// </summary>
        public int SampleRate { get; set; } = 0;

        /// <summary>
        /// Channels.
        /// </summary>
        public int Channels { get; set; } = 0;

        /// <summary>
        /// Bits per sample.
        /// </summary>
        public int BitsPerSample { get; set; } = 0;

        /// <summary>
        /// Offset of the PCM data.
        /// </summary>
        public int DataOffset { get; set; } = 0;

        /// <summary>
        /// PCM data bytes.
        /// </summary>
        public int DataLength { get; set; } = 0;

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        public double DurationSeconds
        {
            get { return SampleRate <= 0 || Channels <= 0 || BitsPerSample <= 0 ? 0 : DataLength / (double)(SampleRate * Channels * (BitsPerSample / 8)); }
        }
    }
}
