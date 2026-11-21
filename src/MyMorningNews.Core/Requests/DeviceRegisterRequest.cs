namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Register a mobile device for push notifications.
    /// </summary>
    public class DeviceRegisterRequest
    {
        /// <summary>
        /// Expo push token, for example ExponentPushToken[...].
        /// </summary>
        [TextLimit(256)]
        public string ExpoPushToken { get; set; } = String.Empty;

        /// <summary>
        /// Platform: ios or android.
        /// </summary>
        [TextLimit(16)]
        public string Platform { get; set; } = String.Empty;

        /// <summary>
        /// Device name shown to the user. Null for none.
        /// </summary>
        [TextLimit(128)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Device locale. Null for none.
        /// </summary>
        [TextLimit(32)]
        public string? Locale { get; set; } = null;
    }
}
