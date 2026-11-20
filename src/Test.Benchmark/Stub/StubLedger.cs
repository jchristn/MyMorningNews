namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// What the stub saw and answered: chat requests (with guardrail presence), quotes it returned, and request counts
    /// by route. The scorer reads it for evidence and guardrail probes. Thread safety: safe for concurrent use.
    /// </summary>
    public class StubLedger
    {
        #region Public-Members

        /// <summary>
        /// Chat requests received.
        /// </summary>
        public ConcurrentQueue<StubChatRecord> Chats { get; set; } = new ConcurrentQueue<StubChatRecord>();

        /// <summary>
        /// Quotes returned during extraction.
        /// </summary>
        public ConcurrentQueue<StubQuoteRecord> Quotes { get; set; } = new ConcurrentQueue<StubQuoteRecord>();

        /// <summary>
        /// Requests by route kind (chat, embeddings, search, page, robots, speech, other).
        /// </summary>
        public ConcurrentDictionary<string, int> Requests { get; set; } = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Tag stamped on every record from now on (the workspace being run). Runs that must be attributed are run one
        /// at a time. Default empty.
        /// </summary>
        public string CurrentTag
        {
            get { return Volatile.Read(ref _CurrentTag); }
            set { Volatile.Write(ref _CurrentTag, value ?? String.Empty); }
        }

        #endregion

        #region Private-Members

        private string _CurrentTag = String.Empty;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Count a request.
        /// </summary>
        /// <param name="kind">Route kind.</param>
        public void Count(string kind)
        {
            ArgumentNullException.ThrowIfNull(kind);
            Requests.AddOrUpdate(kind, 1, (k, v) => v + 1);
        }

        /// <summary>
        /// Chat requests stamped with a tag.
        /// </summary>
        /// <param name="tag">Tag.</param>
        /// <returns>Records.</returns>
        public List<StubChatRecord> ChatsFor(string tag)
        {
            return Chats.Where(c => String.Equals(c.Tag, tag, StringComparison.Ordinal)).ToList();
        }

        /// <summary>
        /// Quotes returned under a tag.
        /// </summary>
        /// <param name="tag">Tag.</param>
        /// <returns>Records.</returns>
        public List<StubQuoteRecord> QuotesFor(string tag)
        {
            return Quotes.Where(q => String.Equals(q.Tag, tag, StringComparison.Ordinal)).ToList();
        }

        /// <summary>
        /// Requests of one kind.
        /// </summary>
        /// <param name="kind">Route kind.</param>
        /// <returns>Count.</returns>
        public int CountOf(string kind)
        {
            return Requests.TryGetValue(kind, out int n) ? n : 0;
        }

        #endregion
    }
}
