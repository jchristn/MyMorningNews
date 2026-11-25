namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using MyMorningNews.Core.Settings;

    /// <summary>Conservative response identity, distinct from research URL deduplication. Thread safe.</summary>
    public static class CrawlCacheKey
    {
        /// <summary>Normalize host/scheme/port, preserving path and query semantics. Null means ineligible.</summary>
        /// <param name="url">Requested absolute HTTP URL, at most 8192 characters.</param>
        /// <returns>Normalized URL without fragment, or null for credentials, sensitive queries, or unsupported URLs.</returns>
        public static string? Normalize(string? url)
        {
            if (String.IsNullOrWhiteSpace(url) || url.Length > 8192 || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != "http" && uri.Scheme != "https") || !String.IsNullOrEmpty(uri.UserInfo)) return null;
            foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string key = Uri.UnescapeDataString(pair.Split('=')[0]).ToLowerInvariant();
                if (new[] { "token", "key", "secret", "signature", "credential", "password", "auth", "session", "jwt", "sig", "x-amz-", "x-goog-" }.Any(s => key.Contains(s, StringComparison.Ordinal))) return null;
            }
            return uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped).ToLowerInvariant()
                + uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
        }

        /// <summary>SHA256 identity. Default conversion profile is v1; change it when conversion behavior changes.</summary>
        /// <param name="settings">Current search settings.</param>
        /// <returns>Profile hash.</returns>
        public static string Profile(SearchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return Hash("crawl-v1\n" + settings.UserAgent + "\nCrawlSharp/1.2.3\nDocConverter/0.1.3");
        }

        /// <summary>Hash UTF-8 input.</summary>
        /// <param name="value">Non-null input.</param>
        /// <returns>Lowercase SHA256 hex.</returns>
        public static string Hash(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }
    }
}
