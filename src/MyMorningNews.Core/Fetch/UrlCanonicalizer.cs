namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Canonical URLs for de-duplication: lowercase scheme and host, no default port, no fragment, no tracking parameters
    /// (utm_*, fbclid, gclid, and similar), remaining query parameters sorted, and no trailing slash except the root.
    /// Thread safety: thread safe.
    /// </summary>
    public static class UrlCanonicalizer
    {
        private static readonly HashSet<string> _Tracking = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fbclid", "gclid", "dclid", "msclkid", "mc_cid", "mc_eid", "ref", "ref_src", "ref_url", "igshid", "_hsenc", "_hsmi", "mkt_tok", "yclid", "spm", "cmpid", "s_kwcid"
        };

        /// <summary>
        /// Canonicalize a URL.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>Canonical form, or null when the value is not an absolute http or https URL.</returns>
        public static string? Canonicalize(string? url)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)) return null;
            string host = u.IdnHost.ToLowerInvariant();
            string port = u.IsDefaultPort ? String.Empty : ":" + u.Port;
            string path = u.AbsolutePath;
            if (path.Length > 1) path = path.TrimEnd('/');
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            foreach (string part in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                string key = WebUtility.UrlDecode(eq < 0 ? part : part.Substring(0, eq));
                string value = eq < 0 ? String.Empty : WebUtility.UrlDecode(part.Substring(eq + 1));
                if (key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || _Tracking.Contains(key)) continue;
                pairs.Add(new KeyValuePair<string, string>(key, value));
            }

            string query = pairs.Count == 0 ? String.Empty : "?" + String.Join("&", pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal)
                .Select(p => WebUtility.UrlEncode(p.Key) + (p.Value.Length == 0 ? String.Empty : "=" + WebUtility.UrlEncode(p.Value))));
            return u.Scheme.ToLowerInvariant() + "://" + host + port + path + query;
        }

        /// <summary>
        /// A stable hash for de-duplication that also ignores the scheme and a leading "www.".
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>Lowercase hex SHA-256, or null when the URL is invalid.</returns>
        public static string? Hash(string? url)
        {
            string? canonical = Canonicalize(url);
            if (canonical == null) return null;
            string key = canonical.Substring(canonical.IndexOf("://", StringComparison.Ordinal) + 3);
            if (key.StartsWith("www.", StringComparison.Ordinal)) key = key.Substring(4);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        }

        /// <summary>
        /// Registrable host of a URL without "www.", for domain rules.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>Host, or null when invalid.</returns>
        public static string? Domain(string? url)
        {
            if (!Uri.TryCreate(url ?? String.Empty, UriKind.Absolute, out Uri? u)) return null;
            string host = u.IdnHost.ToLowerInvariant();
            return host.StartsWith("www.", StringComparison.Ordinal) ? host.Substring(4) : host;
        }

        /// <summary>
        /// Whether a URL's host is a domain or one of its subdomains.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="domain">Domain such as example.com.</param>
        /// <returns>True when it matches.</returns>
        public static bool IsInDomain(string? url, string? domain)
        {
            string? host = Domain(url);
            string d = (domain ?? String.Empty).Trim().ToLowerInvariant().TrimStart('.');
            if (d.StartsWith("www.", StringComparison.Ordinal)) d = d.Substring(4);
            return host != null && d.Length > 0 && (host == d || host.EndsWith("." + d, StringComparison.Ordinal));
        }
    }
}
