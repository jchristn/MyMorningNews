namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>Apple App Store Server Notifications V2 envelope.</summary>
    public class AppleNotificationRequest
    {
        /// <summary>Apple-signed notification. The chain, signature, application and environment are verified by the server.</summary>
        public string SignedPayload { get; set; } = String.Empty;
    }
}
