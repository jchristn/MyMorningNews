namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Minimal PCM WAV support: parse headers, measure duration, concatenate same-format files, and write a tone (tests).
    /// Thread safety: stateless.
    /// </summary>
    public static class WavAudio
    {
        /// <summary>
        /// Parse a RIFF/WAVE PCM header.
        /// </summary>
        /// <param name="data">File bytes.</param>
        /// <returns>Header, or null when the data is not PCM WAV.</returns>
        public static WavInfo? Parse(byte[] data)
        {
            if (data == null || data.Length < 44) return null;
            if (Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || Encoding.ASCII.GetString(data, 8, 4) != "WAVE") return null;
            WavInfo info = new WavInfo();
            int pos = 12;
            bool fmt = false;
            while (pos + 8 <= data.Length)
            {
                string id = Encoding.ASCII.GetString(data, pos, 4);
                int size = BitConverter.ToInt32(data, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    if (body + 16 > data.Length) return null;
                    short format = BitConverter.ToInt16(data, body);
                    if (format != 1 && format != -2) return null;
                    info.Channels = BitConverter.ToInt16(data, body + 2);
                    info.SampleRate = BitConverter.ToInt32(data, body + 4);
                    info.BitsPerSample = BitConverter.ToInt16(data, body + 14);
                    fmt = true;
                }
                else if (id == "data")
                {
                    info.DataOffset = body;
                    // Streaming encoders may write 0 or 0xFFFFFFFF; use what is present.
                    info.DataLength = size <= 0 || body + size > data.Length ? data.Length - body : size;
                    return fmt ? info : null;
                }

                pos = body + Math.Max(0, size) + (size % 2);
            }

            return null;
        }

        /// <summary>
        /// Concatenate PCM WAV files that share sample rate, channels, and bit depth.
        /// </summary>
        /// <param name="files">WAV files.</param>
        /// <returns>One WAV file.</returns>
        /// <exception cref="ArgumentException">A file is not PCM WAV or formats differ.</exception>
        public static byte[] Concatenate(IReadOnlyList<byte[]> files)
        {
            ArgumentNullException.ThrowIfNull(files);
            if (files.Count == 0) throw new ArgumentException("No audio to join.", nameof(files));
            WavInfo first = Parse(files[0]) ?? throw new ArgumentException("Segment 1 is not PCM WAV.", nameof(files));
            using (MemoryStream pcm = new MemoryStream())
            {
                for (int i = 0; i < files.Count; i++)
                {
                    WavInfo w = Parse(files[i]) ?? throw new ArgumentException("Segment " + (i + 1) + " is not PCM WAV.", nameof(files));
                    if (w.SampleRate != first.SampleRate || w.Channels != first.Channels || w.BitsPerSample != first.BitsPerSample)
                        throw new ArgumentException("Segment " + (i + 1) + " has a different audio format.", nameof(files));
                    pcm.Write(files[i], w.DataOffset, w.DataLength);
                }

                return Build(first.SampleRate, first.Channels, first.BitsPerSample, pcm.ToArray());
            }
        }

        /// <summary>
        /// Build a PCM WAV file.
        /// </summary>
        /// <param name="sampleRate">Sample rate.</param>
        /// <param name="channels">Channels.</param>
        /// <param name="bits">Bits per sample.</param>
        /// <param name="pcm">PCM data.</param>
        /// <returns>WAV bytes.</returns>
        public static byte[] Build(int sampleRate, int channels, int bits, byte[] pcm)
        {
            ArgumentNullException.ThrowIfNull(pcm);
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                int blockAlign = channels * bits / 8;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + pcm.Length);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(sampleRate * blockAlign);
                w.Write((short)blockAlign);
                w.Write((short)bits);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(pcm.Length);
                w.Write(pcm);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// A mono 16-bit sine tone (for tests and stubs).
        /// </summary>
        /// <param name="seconds">Length in seconds.</param>
        /// <param name="frequency">Frequency in Hz.</param>
        /// <param name="sampleRate">Sample rate. Default 24000 (Kokoro's rate).</param>
        /// <returns>WAV bytes.</returns>
        public static byte[] Tone(double seconds, double frequency, int sampleRate = 24000)
        {
            int samples = (int)Math.Max(1, seconds * sampleRate);
            byte[] pcm = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                short v = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * 6000);
                pcm[i * 2] = (byte)(v & 0xFF);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }

            return Build(sampleRate, 1, 16, pcm);
        }
    }
}
