namespace MyMorningNews.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Safety net for cleanup: cases register a delete for everything they create and unregister it once they delete it
    /// themselves. Whatever is left runs at the end, newest first, so a failed case does not leave data behind.
    /// Thread safety: guarded by a lock.
    /// </summary>
    public class CleanupRegistry
    {
        private readonly object _Lock = new object();
        private readonly List<KeyValuePair<string, Func<CancellationToken, Task>>> _Actions = new List<KeyValuePair<string, Func<CancellationToken, Task>>>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CleanupRegistry()
        {
        }

        /// <summary>
        /// Number of pending actions.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_Lock) return _Actions.Count;
            }
        }

        /// <summary>
        /// Register a cleanup action.
        /// </summary>
        /// <param name="key">Unique key (for example topic:top_...).</param>
        /// <param name="action">Delete action.</param>
        /// <exception cref="ArgumentNullException">key or action is null.</exception>
        public void Add(string key, Func<CancellationToken, Task> action)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(action);
            lock (_Lock) _Actions.Add(new KeyValuePair<string, Func<CancellationToken, Task>>(key, action));
        }

        /// <summary>
        /// Remove a cleanup action after the case deleted the item itself.
        /// </summary>
        /// <param name="key">Key.</param>
        public void Remove(string key)
        {
            lock (_Lock) _Actions.RemoveAll(a => a.Key == key);
        }

        /// <summary>
        /// Run every remaining action, newest first. 404 responses count as already cleaned.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Keys whose cleanup failed.</returns>
        public async Task<List<string>> RunAsync(CancellationToken token)
        {
            List<KeyValuePair<string, Func<CancellationToken, Task>>> pending;
            lock (_Lock)
            {
                pending = new List<KeyValuePair<string, Func<CancellationToken, Task>>>(_Actions);
                _Actions.Clear();
            }

            pending.Reverse();
            List<string> failed = new List<string>();
            foreach (KeyValuePair<string, Func<CancellationToken, Task>> a in pending)
            {
                try
                {
                    await a.Value(token).ConfigureAwait(false);
                }
                catch (MyMorningNewsException ex) when (ex.StatusCode == 404)
                {
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    failed.Add(a.Key + " (" + ex.Message + ")");
                }
            }

            return failed;
        }
    }
}
