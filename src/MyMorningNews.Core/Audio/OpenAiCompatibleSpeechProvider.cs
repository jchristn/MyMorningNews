namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// POST {endpoint}/audio/speech with {model, input, voice, speed, response_format}. Works with Kokoro-FastAPI,
    /// OpenAI, and compatible servers. Thread safety: safe for concurrent use.
    /// </summary>
    public class OpenAiCompatibleSpeechProvider : ITtsProvider, IDisposable
    {
        private readonly HttpClient _Http;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OpenAiCompatibleSpeechProvider()
        {
            _Http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        }

        /// <inheritdoc />
        public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (String.IsNullOrWhiteSpace(request.Text)) throw new ArgumentException("Nothing to speak.", nameof(request));
            string url = request.Endpoint.TrimEnd('/') + "/audio/speech";
            string body = JsonSerializer.Serialize(new
            {
                model = request.Model,
                input = request.Text,
                voice = request.Voice,
                speed = Math.Clamp(request.Speed, 0.5, 2.0),
                response_format = request.Format
            });
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, url))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, request.TimeoutSeconds)));
                msg.Content = new StringContent(body, Encoding.UTF8, "application/json");
                if (!String.IsNullOrWhiteSpace(request.ApiKey)) msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
                try
                {
                    using (HttpResponseMessage resp = await _Http.SendAsync(msg, timeout.Token).ConfigureAwait(false))
                    {
                        byte[] data = await resp.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                        {
                            string detail = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 300));
                            throw new TtsException("The speech endpoint returned " + (int)resp.StatusCode + ": " + detail, (int)resp.StatusCode);
                        }

                        if (data.Length == 0) throw new TtsException("The speech endpoint returned no audio.");
                        return new TtsResult { Audio = data, ContentType = resp.Content.Headers.ContentType?.MediaType ?? (request.Format == "mp3" ? "audio/mpeg" : "audio/wav") };
                    }
                }
                catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                {
                    throw new TtsException("The speech endpoint did not answer within " + request.TimeoutSeconds + " seconds.", 0, ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new TtsException("Could not reach the speech endpoint at " + request.Endpoint + ": " + ex.Message, 0, ex);
                }
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Http.Dispose();
            _Disposed = true;
        }
    }
}
