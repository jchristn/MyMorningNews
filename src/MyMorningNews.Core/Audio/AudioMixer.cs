namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// Joins synthesized WAV segments into one episode. With ffmpeg, the joined audio is loudness-normalized and encoded as
    /// MP3; without it, the episode is stored as WAV. Segment durations come from the WAV headers, so chapters are exact.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class AudioMixer
    {
        private readonly TtsSettings _Settings;
        private bool? _FfmpegAvailable = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">TTS settings.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public AudioMixer(TtsSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Whether ffmpeg runs (checked once).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when available.</returns>
        public async Task<bool> FfmpegAvailableAsync(CancellationToken token = default)
        {
            if (_FfmpegAvailable.HasValue) return _FfmpegAvailable.Value;
            try
            {
                ProcessResult r = await RunAsync(new List<string> { "-hide_banner", "-version" }, TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
                _FfmpegAvailable = r.ExitCode == 0;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is FileNotFoundException)
            {
                _FfmpegAvailable = false;
            }

            return _FfmpegAvailable.Value;
        }

        /// <summary>
        /// Join segments.
        /// </summary>
        /// <param name="segments">WAV segments in order.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Mixed audio with segment durations.</returns>
        /// <exception cref="ArgumentException">A segment is not PCM WAV or formats differ.</exception>
        /// <exception cref="InvalidOperationException">ffmpeg failed.</exception>
        public async Task<MixResult> MixAsync(IReadOnlyList<byte[]> segments, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(segments);
            List<double> durations = segments.Select(s => (WavAudio.Parse(s) ?? throw new ArgumentException("A speech segment is not PCM WAV.", nameof(segments))).DurationSeconds).ToList();
            byte[] wav = WavAudio.Concatenate(segments);
            double total = durations.Sum();
            if (!await FfmpegAvailableAsync(token).ConfigureAwait(false))
                return new MixResult { Audio = wav, ContentType = "audio/wav", Extension = "wav", DurationSeconds = total, SegmentDurations = durations };

            string dir = Path.Combine(Path.GetTempPath(), "mmn-audio-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string input = Path.Combine(dir, "in.wav");
                string output = Path.Combine(dir, "out.mp3");
                await File.WriteAllBytesAsync(input, wav, token).ConfigureAwait(false);
                List<string> args = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", input };
                if (_Settings.NormalizeLoudness) { args.Add("-af"); args.Add("loudnorm=I=-16:TP=-1.5:LRA=11"); }
                args.AddRange(new[] { "-ac", "1", "-ar", "44100", "-codec:a", "libmp3lame", "-b:a", _Settings.Bitrate, output });
                ProcessResult r = await RunAsync(args, TimeSpan.FromMinutes(10), token).ConfigureAwait(false);
                if (r.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("ffmpeg could not encode the episode: " + r.Error.Trim());
                return new MixResult { Audio = await File.ReadAllBytesAsync(output, token).ConfigureAwait(false), ContentType = "audio/mpeg", Extension = "mp3", DurationSeconds = total, SegmentDurations = durations };
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private async Task<ProcessResult> RunAsync(List<string> args, TimeSpan timeout, CancellationToken token)
        {
            ProcessStartInfo psi = new ProcessStartInfo(_Settings.FfmpegPath) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (string a in args) psi.ArgumentList.Add(a);
            using (Process p = new Process { StartInfo = psi })
            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(timeout);
                p.Start();
                Task<string> err = p.StandardError.ReadToEndAsync(cts.Token);
                Task<string> outp = p.StandardOutput.ReadToEndAsync(cts.Token);
                try
                {
                    await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        p.Kill(true);
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    throw;
                }

                return new ProcessResult { ExitCode = p.ExitCode, Error = await err.ConfigureAwait(false), Output = await outp.ConfigureAwait(false) };
            }
        }
    }
}
