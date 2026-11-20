namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Writes a mono 16-bit PCM WAV tone, the stub's stand-in for synthesized speech.
    /// </summary>
    public static class WavWriter
    {
        #region Public-Methods

        /// <summary>
        /// A sine tone.
        /// </summary>
        /// <param name="seconds">Duration in seconds, 0.05 to 600.</param>
        /// <param name="frequency">Frequency in hertz.</param>
        /// <param name="sampleRate">Sample rate, 8000 to 48000.</param>
        /// <returns>WAV bytes.</returns>
        public static byte[] Tone(double seconds, double frequency, int sampleRate)
        {
            double duration = Math.Clamp(seconds, 0.05, 600);
            int rate = Math.Clamp(sampleRate, 8000, 48000);
            int samples = (int)(duration * rate);
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms, Encoding.ASCII, true))
                {
                    int dataBytes = samples * 2;
                    w.Write(Encoding.ASCII.GetBytes("RIFF"));
                    w.Write(36 + dataBytes);
                    w.Write(Encoding.ASCII.GetBytes("WAVE"));
                    w.Write(Encoding.ASCII.GetBytes("fmt "));
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)1);
                    w.Write(rate);
                    w.Write(rate * 2);
                    w.Write((short)2);
                    w.Write((short)16);
                    w.Write(Encoding.ASCII.GetBytes("data"));
                    w.Write(dataBytes);
                    for (int i = 0; i < samples; i++) w.Write((short)(Math.Sin(2 * Math.PI * frequency * i / rate) * 6000));
                }

                return ms.ToArray();
            }
        }

        #endregion
    }
}
