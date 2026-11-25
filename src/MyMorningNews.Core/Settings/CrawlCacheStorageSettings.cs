namespace MyMorningNews.Core.Settings
{
    using System;
    using System.IO;

    /// <summary>Blobject storage configuration. All changes require restart; S3 requires an explicit key pair.</summary>
    public class CrawlCacheStorageSettings
    {
        /// <summary>Disk or S3. Default "Disk".</summary>
        [Setting(Description = "Disk or S3.", RestartRequired = true, Secret = false, EnvironmentVariable = "CRAWL_CACHE_PROVIDER")]
        public string Provider { get; set; } = "Disk";

        /// <summary>Private persistent cache directory. Default "./crawl-cache".</summary>
        [Setting(Description = "Private persistent cache directory.", RestartRequired = true, Secret = false, EnvironmentVariable = "CRAWL_CACHE_DIRECTORY")]
        public string Directory { get; set; } = "./crawl-cache";

        /// <summary>Custom S3 endpoint, or empty for AWS. Default "".</summary>
        [Setting(Description = "Custom S3 endpoint, or empty for AWS.", RestartRequired = true, Secret = false, EnvironmentVariable = "CRAWL_CACHE_S3_ENDPOINT")]
        public string S3Endpoint { get; set; } = "";

        /// <summary>S3 region. Default "us-west-1".</summary>
        [Setting(Description = "S3 region.", RestartRequired = true, Secret = false, EnvironmentVariable = "CRAWL_CACHE_S3_REGION")]
        public string S3Region { get; set; } = "us-west-1";

        /// <summary>Private S3 bucket. Default "".</summary>
        [Setting(Description = "Private S3 bucket.", RestartRequired = true, Secret = false, EnvironmentVariable = "CRAWL_CACHE_S3_BUCKET")]
        public string S3Bucket { get; set; } = "";

        /// <summary>Dedicated S3 key prefix. Default "mymorningnews/crawl-cache/".</summary>
        [Setting(Description = "Dedicated S3 key prefix.", RestartRequired = true, Secret = false, EnvironmentVariable = "CRAWL_CACHE_S3_PREFIX")]
        public string S3Prefix { get; set; } = "mymorningnews/crawl-cache/";

        /// <summary>Explicit S3 access key. Default "".</summary>
        [Setting(Description = "Explicit S3 access key.", RestartRequired = true, Secret = true, EnvironmentVariable = "CRAWL_CACHE_S3_ACCESS_KEY")]
        public string S3AccessKey { get; set; } = "";

        /// <summary>Explicit S3 secret key. Default "".</summary>
        [Setting(Description = "Explicit S3 secret key.", RestartRequired = true, Secret = true, EnvironmentVariable = "CRAWL_CACHE_S3_SECRET_KEY")]
        public string S3SecretKey { get; set; } = "";

        /// <summary>Validate the provider and paths; require credentials when S3 caching is enabled.</summary>
        /// <param name="enabled">Whether this configuration will be used for caching.</param>
        /// <exception cref="ArgumentException">Configuration is invalid.</exception>
        public void Validate(bool enabled)
        {
            if (Provider != "Disk" && Provider != "S3") throw new ArgumentException("Crawl cache provider must be Disk or S3.");
            if (String.IsNullOrWhiteSpace(Directory)) throw new ArgumentException("Crawl cache directory is required.");
            Path.GetFullPath(Directory);
            if (String.IsNullOrWhiteSpace(S3Prefix) || S3Prefix.StartsWith("/", StringComparison.Ordinal) || S3Prefix.Contains("..", StringComparison.Ordinal) || S3Prefix.Contains('\\'))
                throw new ArgumentException("Crawl cache S3 prefix must be a safe relative dedicated prefix.");
            if (!String.IsNullOrEmpty(S3Endpoint) && (!Uri.TryCreate(S3Endpoint, UriKind.Absolute, out Uri? endpoint) || (endpoint.Scheme != "https" && endpoint.Scheme != "http") || !String.IsNullOrEmpty(endpoint.UserInfo)))
                throw new ArgumentException("Crawl cache S3 endpoint must be an HTTP or HTTPS URL without credentials.");
            if (enabled && Provider == "S3" && (String.IsNullOrWhiteSpace(S3Bucket) || String.IsNullOrWhiteSpace(S3Region) || String.IsNullOrWhiteSpace(S3AccessKey) || String.IsNullOrWhiteSpace(S3SecretKey)))
                throw new ArgumentException("Enabled S3 crawl caching requires bucket, region, access key and secret key; anonymous access is not supported.");
        }
    }
}
