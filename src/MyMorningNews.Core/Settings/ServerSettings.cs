namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Root settings object loaded from mymorningnews.json and overridden by environment variables.
    /// </summary>
    public class ServerSettings
    {
        #region Public-Members

        /// <summary>
        /// UTC time the settings file was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// REST listener settings.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public RestSettings Rest
        {
            get
            {
                return _Rest;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _Rest = value;
            }
        }

        #endregion

        #region Private-Members

        private RestSettings _Rest = new RestSettings();

        #endregion
    }
}
