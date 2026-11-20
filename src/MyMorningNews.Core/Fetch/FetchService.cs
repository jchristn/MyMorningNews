namespace MyMorningNews.Core.Fetch
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using CrawlSharp.Web;
    using DocConverter;
    using DocConverter.Enums;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Fetches single pages for the research pipeline (plan section 15): robots.txt is checked first (cached per host),
    /// requests to one domain are spaced by the configured delay, the page is downloaded with CrawlSharp in single-page
    /// mode, and HTML or PDF is converted to Markdown with DocConverter.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class FetchService : IDisposable
    {
        #region Private-Members

        private static readonly Regex _TitleTag = new Regex("<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _Boilerplate = new Regex("<(script|style|noscript|nav|header|footer|aside|form|svg|iframe)\\b[^>]*>.*?</\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _Main = new Regex("<(main|article)\\b[^>]*>(.*)</\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _SkipLinks = new Regex("^\\s*\\[(Skip|Jump) to [^\\]]*\\]\\([^)]*\\)\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _Http;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _DomainGates = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _LastFetch = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, CachedRobots> _Robots = new ConcurrentDictionary<string, CachedRobots>(StringComparer.OrdinalIgnoreCase);
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings (the Search group is read on every fetch).</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public FetchService(ServerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Http = new HttpClient(Security.NetworkGuard.CreateHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Fetch a page.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="blockedDomains">Domains never fetched (topic and tenant rules).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result with a disposition; never throws for page-level problems.</returns>
        public async Task<FetchResult> FetchAsync(string url, IEnumerable<string>? blockedDomains, CancellationToken token = default)
        {
            Stopwatch sw = Stopwatch.StartNew();
            FetchResult result = new FetchResult { Url = url ?? String.Empty, CanonicalUrl = UrlCanonicalizer.Canonicalize(url) };
            try
            {
                if (result.CanonicalUrl == null)
                {
                    result.Disposition = FetchDispositionEnum.Unsupported;
                    result.Error = "Not an absolute http or https URL.";
                    return result;
                }

                if (blockedDomains != null && blockedDomains.Any(d => UrlCanonicalizer.IsInDomain(url, d)))
                {
                    result.Disposition = FetchDispositionEnum.Blocked;
                    result.Error = "The domain is blocked.";
                    return result;
                }

                Uri uri = new Uri(url!);
                try
                {
                    await Security.NetworkGuard.EnsureAllowedAsync(url, token).ConfigureAwait(false);
                }
                catch (Exceptions.EgressBlockedException ex)
                {
                    result.Disposition = FetchDispositionEnum.Blocked;
                    result.Error = ex.Message;
                    return result;
                }

                SearchSettings s = _Settings.Search;
                if (s.RespectRobots && !await RobotsAllowAsync(uri, s, token).ConfigureAwait(false))
                {
                    result.Disposition = FetchDispositionEnum.RobotsDenied;
                    result.Error = "robots.txt disallows this page.";
                    return result;
                }

                await WaitForDomainAsync(uri.Host, s.PerDomainDelayMs, token).ConfigureAwait(false);
                try
                {
                    await DownloadAsync(uri, s, result, token).ConfigureAwait(false);
                }
                finally
                {
                    _LastFetch[uri.Host] = DateTime.UtcNow;
                    if (_DomainGates.TryGetValue(uri.Host, out SemaphoreSlim? gate)) gate.Release();
                }

                return result;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                result.Disposition = FetchDispositionEnum.Failed;
                result.Error = "Timed out.";
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                result.Disposition = FetchDispositionEnum.Failed;
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                result.DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.FetchRequests, "outcome", result.Disposition.ToString());
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.FetchDuration, sw.Elapsed.TotalSeconds);
            }
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _Http.Dispose();
                foreach (SemaphoreSlim gate in _DomainGates.Values) gate.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task DownloadAsync(Uri uri, SearchSettings s, FetchResult result, CancellationToken token)
        {
            CrawlSharp.Web.Settings settings = new CrawlSharp.Web.Settings();
            settings.Crawl.StartUrl = uri.ToString();
            settings.Crawl.UserAgent = s.UserAgent;
            settings.Crawl.FollowLinks = false;
            settings.Crawl.IncludeSitemap = false;
            settings.Crawl.MaxCrawlDepth = 0;
            settings.Crawl.IgnoreRobotsText = true;
            settings.Crawl.PageTimeoutMs = s.FetchTimeoutMs;
            settings.Crawl.FollowRedirects = true;
            settings.Crawl.RestrictToChildUrls = false;
            settings.Crawl.RestrictToSameSubdomain = false;
            settings.Crawl.RestrictToSameRootDomain = false;
            settings.Crawl.RequestDelayMs = 0;
            settings.Crawl.MaxRetries = 1;
            settings.Crawl.RetryMinBackoffMs = 500;
            settings.Crawl.RetryMaxBackoffMs = 2000;

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(s.FetchTimeoutMs + 5000);
                WebResource? page = null;
                using (WebCrawler crawler = new WebCrawler(settings, timeout.Token))
                {
                    await foreach (WebResource r in crawler.CrawlAsync(timeout.Token).ConfigureAwait(false))
                    {
                        page = r;
                        break;
                    }
                }

                if (page == null)
                {
                    result.Disposition = FetchDispositionEnum.Failed;
                    result.Error = "No response.";
                    return;
                }

                result.StatusCode = page.Status;
                result.FinalUrl = String.IsNullOrEmpty(page.FinalUrl) ? page.Url : page.FinalUrl;
                result.CanonicalUrl = UrlCanonicalizer.Canonicalize(result.FinalUrl) ?? result.CanonicalUrl;
                result.ContentType = page.ContentType;
                byte[] data = page.Data ?? Array.Empty<byte>();
                result.Bytes = data.LongLength;
                if (page.Status < 200 || page.Status >= 300)
                {
                    result.Disposition = FetchDispositionEnum.HttpError;
                    result.Error = "HTTP " + page.Status + ".";
                    return;
                }

                if (data.LongLength > s.MaxFetchBytes)
                {
                    result.Disposition = FetchDispositionEnum.TooLarge;
                    result.Error = "Larger than " + s.MaxFetchBytes + " bytes.";
                    return;
                }

                result.ContentHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                string type = (page.ContentType ?? String.Empty).ToLowerInvariant();
                if (type.Contains("html", StringComparison.Ordinal) || (type.Length == 0 && LooksLikeHtml(data)))
                {
                    string html = Encoding.UTF8.GetString(data);
                    Match m = _TitleTag.Match(html);
                    if (m.Success) result.Title = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                    string text = await ConvertAsync(Encoding.UTF8.GetBytes(MainContent(html)), DocumentFormatEnum.Html, timeout.Token).ConfigureAwait(false);
                    result.Text = _SkipLinks.Replace(text, String.Empty).Trim();
                }
                else if (type.Contains("pdf", StringComparison.Ordinal))
                {
                    result.Text = await ConvertAsync(data, DocumentFormatEnum.Pdf, timeout.Token).ConfigureAwait(false);
                }
                else if (type.StartsWith("text/", StringComparison.Ordinal) || type.Contains("json", StringComparison.Ordinal) || type.Contains("xml", StringComparison.Ordinal) || type.Contains("markdown", StringComparison.Ordinal))
                {
                    result.Text = Encoding.UTF8.GetString(data);
                }
                else
                {
                    result.Disposition = FetchDispositionEnum.Unsupported;
                    result.Error = "Content type " + page.ContentType + " is not converted.";
                    return;
                }

                result.Disposition = FetchDispositionEnum.Fetched;
            }
        }

        private static async Task<string> ConvertAsync(byte[] data, DocumentFormatEnum from, CancellationToken token)
        {
            using (Converter converter = new Converter())
            using (MemoryStream output = new MemoryStream())
            {
                await converter.ConvertAsync(data, from, DocumentFormatEnum.Markdown, output, null, token).ConfigureAwait(false);
                return Encoding.UTF8.GetString(output.ToArray()).Trim();
            }
        }

        /// <summary>
        /// Reduce a page to its main content: the main or article element when present, without navigation, headers,
        /// footers, asides, forms, scripts, or styles.
        /// </summary>
        /// <param name="html">Page HTML.</param>
        /// <returns>Trimmed HTML.</returns>
        public static string MainContent(string html)
        {
            if (String.IsNullOrEmpty(html)) return String.Empty;
            string cleaned = _Boilerplate.Replace(html, " ");
            Match main = _Main.Match(cleaned);
            if (main.Success && main.Groups[2].Value.Length > 200) return "<html><body>" + main.Groups[2].Value + "</body></html>";
            return cleaned;
        }

        private static bool LooksLikeHtml(byte[] data)
        {
            string head = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 512)).TrimStart().ToLowerInvariant();
            return head.StartsWith("<!doctype html", StringComparison.Ordinal) || head.StartsWith("<html", StringComparison.Ordinal);
        }

        private async Task WaitForDomainAsync(string host, int delayMs, CancellationToken token)
        {
            SemaphoreSlim gate = _DomainGates.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(token).ConfigureAwait(false);
            if (_LastFetch.TryGetValue(host, out DateTime last))
            {
                TimeSpan wait = last.AddMilliseconds(delayMs) - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(wait, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        gate.Release();
                        throw;
                    }
                }
            }
        }

        private async Task<bool> RobotsAllowAsync(Uri uri, SearchSettings s, CancellationToken token)
        {
            string key = uri.Scheme + "://" + uri.Authority;
            if (!_Robots.TryGetValue(key, out CachedRobots? cached) || cached.ExpiresUtc < DateTime.UtcNow)
            {
                RobotsFile? robots = null;
                try
                {
                    using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, key + "/robots.txt"))
                    {
                        timeout.CancelAfter(Math.Min(10000, s.FetchTimeoutMs));
                        request.Headers.TryAddWithoutValidation("User-Agent", s.UserAgent);
                        using (HttpResponseMessage response = await _Http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                        {
                            string type = response.Content.Headers.ContentType?.MediaType ?? "text/plain";
                            if (response.IsSuccessStatusCode && type.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
                                robots = new RobotsFile(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                {
                    robots = null;
                }

                cached = new CachedRobots { Robots = robots, ExpiresUtc = DateTime.UtcNow.AddHours(1) };
                _Robots[key] = cached;
            }

            if (cached.Robots == null) return true;
            string path = uri.PathAndQuery;
            string agent = s.UserAgent.Split('/')[0];
            List<string> disallow = cached.Robots.GetDisallowUrls(agent).Concat(cached.Robots.GetDisallowUrls("*")).Where(d => !String.IsNullOrEmpty(d)).ToList();
            List<string> allow = cached.Robots.GetAllowUrls(agent).Concat(cached.Robots.GetAllowUrls("*")).Where(a => !String.IsNullOrEmpty(a)).ToList();
            int longestDisallow = disallow.Where(d => path.StartsWith(d, StringComparison.Ordinal)).Select(d => d.Length).DefaultIfEmpty(-1).Max();
            int longestAllow = allow.Where(a => path.StartsWith(a, StringComparison.Ordinal)).Select(a => a.Length).DefaultIfEmpty(-1).Max();
            return longestDisallow < 0 || longestAllow >= longestDisallow;
        }

        #endregion
    }
}
