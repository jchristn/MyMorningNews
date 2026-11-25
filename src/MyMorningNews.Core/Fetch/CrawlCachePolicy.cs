namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http.Headers;

    /// <summary>Conservative admission and origin freshness policy for anonymous page responses. Thread safe.</summary>
    public static class CrawlCachePolicy
    {
        /// <summary>Calculate an origin freshness ceiling. Null means the response must not enter the shared cache.</summary>
        /// <param name="headers">Origin response headers.</param>
        /// <param name="startedUtc">Start of this origin request.</param>
        /// <param name="receivedUtc">Completion time.</param>
        /// <param name="freshnessSeconds">Configured upper bound.</param>
        /// <param name="redirected">Redirect chains bypass caching because CrawlSharp omits intermediate cookie/cache headers.</param>
        /// <returns>Expiry, or null for private, uncacheable, malformed or already-stale responses.</returns>
        public static DateTime? Expiry(NameValueCollection headers, DateTime startedUtc, DateTime receivedUtc, int freshnessSeconds, bool redirected)
        {
            ArgumentNullException.ThrowIfNull(headers);
            if (redirected || headers.Get("Set-Cookie") != null) return null;
            string vary = headers.Get("Vary") ?? String.Empty;
            if (vary.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(v => !v.Trim().Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))) return null;
            CacheControlHeaderValue? control = null;
            string? rawControl = headers.Get("Cache-Control");
            if (rawControl != null && !CacheControlHeaderValue.TryParse(rawControl, out control)) return null;
            if (control != null && (control.NoStore || control.Private || control.NoCache)) return null;
            if ((headers.Get("Pragma") ?? String.Empty).Contains("no-cache", StringComparison.OrdinalIgnoreCase)) return null;
            double age = 0;
            if (headers.Get("Age") != null && (!Double.TryParse(headers.Get("Age"), NumberStyles.None, CultureInfo.InvariantCulture, out age) || !Double.IsFinite(age) || age < 0)) return null;
            DateTime date = receivedUtc;
            if (headers.Get("Date") != null)
            {
                if (!DateTimeOffset.TryParse(headers.Get("Date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)) return null;
                date = parsed.UtcDateTime;
            }
            double currentAge = Math.Max(Math.Max(0, (receivedUtc - date).TotalSeconds), age + Math.Max(0, (receivedUtc - startedUtc).TotalSeconds));
            double lifetime = freshnessSeconds;
            TimeSpan? maxAge = control?.SharedMaxAge ?? control?.MaxAge;
            if (maxAge.HasValue) lifetime = Math.Min(lifetime, maxAge.Value.TotalSeconds);
            else if (headers.Get("Expires") != null)
            {
                if (!DateTimeOffset.TryParse(headers.Get("Expires"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset expires)) return null;
                lifetime = Math.Min(lifetime, (expires.UtcDateTime - date).TotalSeconds);
            }
            double remaining = lifetime - currentAge;
            return remaining > 0 ? receivedUtc.AddSeconds(remaining) : null;
        }
    }
}
