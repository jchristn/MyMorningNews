namespace MyMorningNews.Core.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Outcome of a push send.
    /// </summary>
    public class PushResult
    {
        /// <summary>
        /// Devices the message was accepted for.
        /// </summary>
        public int Accepted { get; set; } = 0;

        /// <summary>
        /// Devices that failed.
        /// </summary>
        public int Failed { get; set; } = 0;

        /// <summary>
        /// Devices removed because Expo reported them as no longer registered.
        /// </summary>
        public int Removed { get; set; } = 0;

        /// <summary>
        /// Error messages, one per failed device.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}
