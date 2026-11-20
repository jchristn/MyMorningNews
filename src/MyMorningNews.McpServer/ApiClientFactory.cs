namespace MyMorningNews.McpServer
{
    using System;
    using System.Net.Http;
    using MyMorningNews.Sdk;

    /// <summary>
    /// Builds SDK clients that carry a caller's credentials. One HttpClient is shared by every client, so connections are
    /// pooled; the clients themselves are cheap and are created per call. Thread safe.
    /// </summary>
    public class ApiClientFactory : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// REST base URL.
        /// </summary>
        public string BaseUrl { get; }

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the factory.
        /// </summary>
        /// <param name="baseUrl">REST base URL.</param>
        /// <param name="timeout">Request timeout.</param>
        /// <exception cref="ArgumentNullException">Base URL is null or empty.</exception>
        public ApiClientFactory(string baseUrl, TimeSpan timeout)
        {
            if (String.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));
            BaseUrl = baseUrl.TrimEnd('/');
            _Http = new HttpClient { Timeout = timeout };
            _Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MyMorningNews.McpServer/0.1.0-alpha");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// A client for the caller. Dispose it after the call; the shared HttpClient stays open.
        /// </summary>
        /// <param name="credentials">Caller credentials.</param>
        /// <returns>Client.</returns>
        /// <exception cref="ArgumentNullException">Credentials are null.</exception>
        /// <exception cref="ObjectDisposedException">The factory was disposed.</exception>
        public MyMorningNewsClient Create(CallerCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            ObjectDisposedException.ThrowIf(_Disposed, this);
            MyMorningNewsClient client = new MyMorningNewsClient(BaseUrl, _Http);
            if (!String.IsNullOrEmpty(credentials.Token))
            {
                client.Token = credentials.Token;
            }
            else
            {
                client.AccessKey = credentials.AccessKey;
                client.SecretKey = credentials.SecretKey;
            }

            client.TenantId = credentials.TenantId;
            return client;
        }

        /// <summary>
        /// Dispose the shared HttpClient.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose the shared HttpClient.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Http.Dispose();
            _Disposed = true;
        }

        #endregion
    }
}
