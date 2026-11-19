namespace MyMorningNews.Core.Audio
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Text-to-speech provider (plan section 22).
    /// </summary>
    public interface ITtsProvider
    {
        /// <summary>
        /// Synthesize speech.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Audio.</returns>
        /// <exception cref="TtsException">The provider failed.</exception>
        Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken token = default);
    }
}
