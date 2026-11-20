namespace MyMorningNews.McpServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Models;

    /// <summary>
    /// Credentials for stdio mode, from environment variables: a session token, an access key and secret key, or an
    /// email and password that sign in at startup and again when the session expires. Thread safe.
    /// </summary>
    public class StdioCaller
    {
        #region Private-Members

        private readonly McpServerSettings _Settings;
        private readonly ApiClientFactory _Clients;
        private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private CallerCredentials? _Current = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the caller.
        /// </summary>
        /// <param name="settings">Settings with the stdio credentials.</param>
        /// <param name="clients">Client factory.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public StdioCaller(McpServerSettings settings, ApiClientFactory clients)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Clients = clients ?? throw new ArgumentNullException(nameof(clients));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// True when the settings carry a token, an access key and secret key, or an email and password.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                return !String.IsNullOrEmpty(_Settings.Token) || (!String.IsNullOrEmpty(_Settings.AccessKey) && !String.IsNullOrEmpty(_Settings.SecretKey))
                    || (!String.IsNullOrEmpty(_Settings.Email) && !String.IsNullOrEmpty(_Settings.Password));
            }
        }

        /// <summary>
        /// Whether the credentials can be renewed (email and password sign-in).
        /// </summary>
        public bool CanRenew
        {
            get { return String.IsNullOrEmpty(_Settings.Token) && String.IsNullOrEmpty(_Settings.AccessKey) && !String.IsNullOrEmpty(_Settings.Email) && !String.IsNullOrEmpty(_Settings.Password); }
        }

        /// <summary>
        /// The current credentials, signing in first when needed.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credentials.</returns>
        /// <exception cref="InvalidOperationException">No credentials are configured.</exception>
        public async Task<CallerCredentials> GetAsync(CancellationToken token)
        {
            CallerCredentials? current = _Current;
            if (current != null) return current;
            return await RenewAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Sign in again (email and password), or reload the configured token or access key.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Fresh credentials.</returns>
        /// <exception cref="InvalidOperationException">No credentials are configured.</exception>
        public async Task<CallerCredentials> RenewAsync(CancellationToken token)
        {
            await _Lock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!String.IsNullOrEmpty(_Settings.Token))
                {
                    _Current = new CallerCredentials { Token = _Settings.Token, TenantId = _Settings.TenantId };
                }
                else if (!String.IsNullOrEmpty(_Settings.AccessKey) && !String.IsNullOrEmpty(_Settings.SecretKey))
                {
                    _Current = new CallerCredentials { AccessKey = _Settings.AccessKey, SecretKey = _Settings.SecretKey, TenantId = _Settings.TenantId };
                }
                else if (!String.IsNullOrEmpty(_Settings.Email) && !String.IsNullOrEmpty(_Settings.Password))
                {
                    using (MyMorningNewsClient client = _Clients.Create(new CallerCredentials()))
                    {
                        TokenResponse login = await client.LoginAsync(_Settings.Email, _Settings.Password, _Settings.TenantId, token).ConfigureAwait(false);
                        _Current = new CallerCredentials { Token = login.Token, TenantId = login.TenantId };
                    }
                }
                else
                {
                    throw new InvalidOperationException("Set MYMORNINGNEWS_TOKEN, MYMORNINGNEWS_ACCESS_KEY with MYMORNINGNEWS_SECRET_KEY and MYMORNINGNEWS_TENANT_ID, or MYMORNINGNEWS_EMAIL with MYMORNINGNEWS_PASSWORD.");
                }

                return _Current;
            }
            finally
            {
                _Lock.Release();
            }
        }

        #endregion
    }
}
