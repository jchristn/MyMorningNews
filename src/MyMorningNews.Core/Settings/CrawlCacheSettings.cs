namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>Disposable crawl cache limits. Live fields are read per operation; storage changes require restart.</summary>
    public class CrawlCacheSettings
    {
        /// <summary>Enable cache reads and writes. Default false. Cleanup continues while disabled.</summary>
        [Setting(Description = "Enable temporary crawl caching.", EnvironmentVariable = "CRAWL_CACHE_ENABLED")]
        public bool Enabled { get; set; } = false;

        /// <summary>Storage namespace shared only by replicas with the same backing store. Default crawl-disk-1.</summary>
        [Setting(Description = "Unique namespace for this backing store.", RestartRequired = true, EnvironmentVariable = "CRAWL_CACHE_NAMESPACE")]
        public string StorageNamespace { get; set; } = "crawl-disk-1";

        /// <summary>Disk or S3 storage configuration. Never null.</summary>
        /// <exception cref="ArgumentNullException">Value is null.</exception>
        public CrawlCacheStorageSettings Storage { get { return _Storage; } set { _Storage = value ?? throw new ArgumentNullException(nameof(Storage)); } }
        private CrawlCacheStorageSettings _Storage = new CrawlCacheStorageSettings();

        /// <summary>Maximum content freshness in seconds. Default 86400; range 60 to 604800.</summary>
        [Setting(Description = "Maximum content freshness in seconds.", Minimum = 60, Maximum = 604800, EnvironmentVariable = "CRAWL_CACHE_FRESHNESS_SECONDS")]
        public int FreshnessSeconds { get { return _FreshnessSeconds; } set { _FreshnessSeconds = Math.Clamp(value, 60, 604800); } }
        private int _FreshnessSeconds = 86400;

        /// <summary>Combined raw and Markdown byte limit per entry. Default 8388608; range 65536 to 104857600.</summary>
        [Setting(Description = "Combined raw and Markdown byte limit per entry.", Minimum = 65536, Maximum = 104857600, EnvironmentVariable = "CRAWL_CACHE_MAX_ENTRY_BYTES")]
        public long MaxEntryBytes { get { return _MaxEntryBytes; } set { _MaxEntryBytes = Math.Clamp(value, 65536L, 104857600L); } }
        private long _MaxEntryBytes = 8388608L;

        /// <summary>Maximum reserved content bytes per namespace. Default 10737418240; range 65536 to 1099511627776.</summary>
        [Setting(Description = "Maximum reserved content bytes per namespace.", Minimum = 65536, Maximum = 1099511627776, EnvironmentVariable = "CRAWL_CACHE_MAX_TOTAL_BYTES")]
        public long MaxTotalBytes { get { return _MaxTotalBytes; } set { _MaxTotalBytes = Math.Clamp(value, 65536L, 1099511627776L); } }
        private long _MaxTotalBytes = 10737418240L;

        /// <summary>Maximum entries and generations per namespace. Default 100000; range 1 to 1000000.</summary>
        [Setting(Description = "Maximum entries and generations per namespace.", Minimum = 1, Maximum = 1000000, EnvironmentVariable = "CRAWL_CACHE_MAX_ENTRIES")]
        public int MaxEntries { get { return _MaxEntries; } set { _MaxEntries = Math.Clamp(value, 1, 1000000); } }
        private int _MaxEntries = 100000;

        /// <summary>Seconds between bounded cleanup passes. Default 300; range 30 to 3600.</summary>
        [Setting(Description = "Seconds between bounded cleanup passes.", Minimum = 30, Maximum = 3600, EnvironmentVariable = "CRAWL_CACHE_CLEANUP_INTERVAL_SECONDS")]
        public int CleanupIntervalSeconds { get { return _CleanupIntervalSeconds; } set { _CleanupIntervalSeconds = Math.Clamp(value, 30, 3600); } }
        private int _CleanupIntervalSeconds = 300;

        /// <summary>Maximum generations per cleanup pass. Default 100; range 1 to 1000.</summary>
        [Setting(Description = "Maximum generations per cleanup pass.", Minimum = 1, Maximum = 1000, EnvironmentVariable = "CRAWL_CACHE_CLEANUP_BATCH_SIZE")]
        public int CleanupBatchSize { get { return _CleanupBatchSize; } set { _CleanupBatchSize = Math.Clamp(value, 1, 1000); } }
        private int _CleanupBatchSize = 100;

        /// <summary>Timeout per cache operation in milliseconds. Default 5000; range 100 to 30000.</summary>
        [Setting(Description = "Timeout per cache operation in milliseconds.", Minimum = 100, Maximum = 30000, EnvironmentVariable = "CRAWL_CACHE_OPERATION_TIMEOUT_MS")]
        public int OperationTimeoutMs { get { return _OperationTimeoutMs; } set { _OperationTimeoutMs = Math.Clamp(value, 100, 30000); } }
        private int _OperationTimeoutMs = 5000;

        /// <summary>Fill lease lifetime, renewed during downloads. Default 180; range 30 to 600.</summary>
        [Setting(Description = "Fill lease lifetime, renewed during downloads.", Minimum = 30, Maximum = 600, EnvironmentVariable = "CRAWL_CACHE_LEASE_SECONDS")]
        public int LeaseSeconds { get { return _LeaseSeconds; } set { _LeaseSeconds = Math.Clamp(value, 30, 600); } }
        private int _LeaseSeconds = 180;

        /// <summary>Maximum wait for another fill in milliseconds. Default 5000; range 0 to 30000.</summary>
        [Setting(Description = "Maximum wait for another fill in milliseconds.", Minimum = 0, Maximum = 30000, EnvironmentVariable = "CRAWL_CACHE_WAIT_FOR_FILL_MS")]
        public int WaitForFillMs { get { return _WaitForFillMs; } set { _WaitForFillMs = Math.Clamp(value, 0, 30000); } }
        private int _WaitForFillMs = 5000;

        /// <summary>Maximum concurrent cache object operations. Default 8; range 1 to 64.</summary>
        [Setting(Description = "Maximum concurrent cache object operations.", Minimum = 1, Maximum = 64, EnvironmentVariable = "CRAWL_CACHE_MAX_CONCURRENCY", RestartRequired = true)]
        public int MaxConcurrency { get { return _MaxConcurrency; } set { _MaxConcurrency = Math.Clamp(value, 1, 64); } }
        private int _MaxConcurrency = 8;

        /// <summary>Validate combined limits and enabled storage configuration.</summary>
        /// <exception cref="ArgumentException">Namespace, limits, or enabled storage configuration is invalid.</exception>
        public void Validate()
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(StorageNamespace ?? String.Empty, "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$"))
                throw new ArgumentException("Crawl cache namespace must contain 1 to 64 letters, digits, underscores or hyphens.");
            if (MaxTotalBytes < MaxEntryBytes) throw new ArgumentException("Crawl cache total byte limit must cover one entry.");
            Storage.Validate(Enabled);
        }
    }
}
