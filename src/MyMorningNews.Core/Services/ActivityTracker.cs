namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using SyslogLogging;

    /// <summary>
    /// Records that a signed-in user used the product, for daily, weekly, and monthly active user counts. Writes at
    /// most once per user per UTC day per process (later requests that day are answered from memory), and never
    /// fails the request: errors are logged.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class ActivityTracker
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly ConcurrentDictionary<string, DateOnly> _Seen = new ConcurrentDictionary<string, DateOnly>(StringComparer.Ordinal);
        private int _MaxEntries = 100000;

        /// <summary>
        /// Users remembered before the memory is cleared. Default 100000, minimum 100, maximum 10000000.
        /// </summary>
        public int MaxEntries
        {
            get { return _MaxEntries; }
            set { _MaxEntries = Math.Clamp(value, 100, 10000000); }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ActivityTracker(DatabaseDriverBase database, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Record activity for a user now. Does nothing for an empty tenant or user, or when the user was already
        /// recorded today.
        /// </summary>
        /// <param name="tenantId">Tenant, or null.</param>
        /// <param name="userId">User, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a row was written.</returns>
        public async Task<bool> TouchAsync(string? tenantId, string? userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId) || String.IsNullOrWhiteSpace(userId)) return false;
            DateTime now = DateTime.UtcNow;
            DateOnly today = DateOnly.FromDateTime(now);
            if (_Seen.TryGetValue(userId, out DateOnly seen) && seen == today) return false;
            if (_Seen.Count >= _MaxEntries) _Seen.Clear();
            _Seen[userId] = today;
            try
            {
                await _Database.PlatformStats.TouchAsync(tenantId, userId, now, token).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Seen.TryRemove(userId, out DateOnly _);
                _Logging.Warn("[ActivityTracker] could not record activity for user " + userId + ": " + ex.Message);
                return false;
            }
        }
    }
}
