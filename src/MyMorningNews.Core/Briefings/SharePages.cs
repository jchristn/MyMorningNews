namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Server-rendered public pages for a shared briefing (plan section 36): the reading page, the listening page, and
    /// one page for every link that does not work. Self-contained HTML with inline CSS, light and dark, usable at
    /// 320 px, no JavaScript, no cookies, no third-party requests. Every value is HTML encoded; briefing Markdown goes
    /// through <see cref="SafeMarkdown"/>, which shows raw HTML as text, and outbound links carry
    /// rel="noopener noreferrer nofollow". Thread safety: stateless.
    /// </summary>
    public static class SharePages
    {
        /// <summary>
        /// The rel attribute on every link that leaves the page.
        /// </summary>
        public const string OutboundRel = "noopener noreferrer nofollow";

        /// <summary>
        /// The Content-Security-Policy every share page is served with: no scripts, no frames, no third-party requests.
        /// </summary>
        public const string ContentSecurityPolicy = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

        private const string Css =
            ":root{color-scheme:light dark;--bg:#f8fafc;--card:#ffffff;--fg:#0f172a;--muted:#475569;--line:#e2e8f0;--accent:#2563eb;--button:#2563eb;--button-hover:#1d4ed8;--soft:#eff6ff;--gold:#f2b705;--quote:#f1f5f9}" +
            "@media (prefers-color-scheme:dark){:root{--bg:#0f172a;--card:#111c33;--fg:#e2e8f0;--muted:#94a3b8;--line:#26344f;--accent:#60a5fa;--soft:#16254a;--quote:#16213a}" +
            ".brand img{filter:invert(1) hue-rotate(180deg)}}" +
            "*{box-sizing:border-box}html{background:var(--bg);-webkit-text-size-adjust:100%}" +
            "body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.6 system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif}" +
            "a{color:var(--accent)}a:focus-visible,audio:focus-visible{outline:3px solid var(--accent);outline-offset:3px;border-radius:4px}" +
            ".skip{position:absolute;left:-9999px}.skip:focus{left:12px;top:12px;background:var(--card);padding:8px 12px;border-radius:6px;z-index:2}" +
            ".wrap{max-width:720px;margin:0 auto;padding:0 16px}" +
            "header.top{border-bottom:1px solid var(--line);background:var(--card)}header.top .wrap{display:flex;align-items:center;justify-content:space-between;gap:12px;padding-top:10px;padding-bottom:10px}" +
            ".brand{display:inline-flex;align-items:center}.brand img{display:block;height:36px;width:auto}" +
            ".btn{display:inline-block;padding:10px 18px;border-radius:8px;background:var(--button);color:#ffffff;font-weight:600;font-size:15px;line-height:1.2;text-decoration:none;text-align:center}" +
            ".btn:hover{background:var(--button-hover)}.btn:focus-visible{outline:3px solid var(--accent);outline-offset:3px}.btn.small{padding:8px 14px;font-size:14px;white-space:nowrap}" +
            ".pitch{margin:20px 0 0;padding:12px 14px;border-left:4px solid var(--gold);background:var(--soft);border-radius:0 8px 8px 0;font-size:15px}.pitch p{margin:0}" +
            "main{padding:8px 0 40px}article{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:24px 20px;margin-top:20px}" +
            ".kicker{margin:0 0 6px;font-size:13px;font-weight:600;letter-spacing:.04em;text-transform:uppercase;color:var(--muted)}" +
            "h1{font-size:28px;line-height:1.2;margin:0 0 8px}.meta{margin:0 0 4px;color:var(--muted);font-size:14px}" +
            ".switch{margin:14px 0 0;font-size:15px;font-weight:600}" +
            ".content{margin-top:20px;border-top:1px solid var(--line);padding-top:8px;font:17px/1.65 Georgia,'Times New Roman',serif;overflow-wrap:anywhere}" +
            ".content h1,.content h2,.content h3,.content h4{font-family:system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;line-height:1.25}" +
            ".content h1{font-size:24px;margin:28px 0 8px}.content h2{font-size:21px;margin:28px 0 8px}.content h3{font-size:18px;margin:22px 0 6px}.content li{margin:4px 0}.content ul,.content ol{padding-left:22px}" +
            ".content code{font-family:Menlo,Consolas,monospace;font-size:14px}.content pre{overflow-x:auto;padding:12px;background:var(--quote);border-radius:6px}" +
            ".content blockquote{margin:16px 0;padding:8px 16px;border-left:3px solid var(--accent);background:var(--quote)}.content hr{border:0;border-top:1px solid var(--line);margin:28px 0}" +
            "audio{display:block;width:100%;margin:20px 0 4px}.chapters{margin:20px 0 0}.chapters h2{font-size:17px;margin:0 0 8px}.chapters ol{list-style:none;margin:0;padding:0;border-top:1px solid var(--line)}" +
            ".chapters li{border-bottom:1px solid var(--line)}.chapters a{display:flex;justify-content:space-between;gap:12px;padding:10px 2px;text-decoration:none}.chapters a:hover span:first-child{text-decoration:underline}" +
            ".chapters .time{color:var(--muted);font-variant-numeric:tabular-nums}.chapters a[aria-current]{font-weight:700}" +
            ".notice{margin:20px 0 0;padding:14px;border:1px dashed var(--line);border-radius:8px}" +
            ".cta{margin-top:24px;background:var(--card);border:1px solid var(--line);border-top:4px solid var(--gold);border-radius:12px;padding:24px 20px}" +
            ".cta h2{font-size:22px;line-height:1.25;margin:0 0 8px}.cta p{margin:0 0 12px}.cta ul{margin:0 0 18px;padding:0;list-style:none}.cta li{margin:0 0 10px;padding-left:18px;position:relative}" +
            ".cta li::before{content:'';position:absolute;left:0;top:.6em;width:8px;height:8px;border-radius:50%;background:var(--gold)}.cta .fine{margin:12px 0 0;font-size:13px;color:var(--muted)}" +
            "footer.end{padding:20px 0 36px;font-size:13px;color:var(--muted);text-align:center}footer.end a{color:var(--muted)}" +
            "@media (max-width:480px){h1{font-size:24px}article,.cta{padding:20px 16px}.brand img{height:30px}.content{font-size:16px}}";

        private static readonly Regex _LeadingTitle = new Regex(@"\A\s*#\s+[^\n]*\n?", RegexOptions.Compiled);

        /// <summary>
        /// The shared briefing page: one page to listen and to read. When the briefing has finished audio, a native
        /// audio player with chapter links comes first, followed by the briefing text. Without audio it is the text alone.
        /// </summary>
        /// <param name="shared">The shared briefing.</param>
        /// <param name="links">Page addresses.</param>
        /// <param name="startSeconds">Where playback starts (a chapter link), or null for the beginning. Default null.</param>
        /// <returns>HTML document.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static string Read(SharedBriefing shared, SharePageLinks links, double? startSeconds = null)
        {
            ArgumentNullException.ThrowIfNull(shared);
            ArgumentNullException.ThrowIfNull(links);
            Briefing b = shared.Briefing;
            bool playable = shared.Audio != null && !String.IsNullOrEmpty(links.AudioPath);
            StringBuilder sb = new StringBuilder();
            sb.Append("<article>");
            AppendHeading(sb, shared, "Briefing");
            List<string> meta = new List<string>();
            if (playable && shared.Audio!.DurationSeconds >= 1) meta.Add(Clock(shared.Audio.DurationSeconds) + " of audio");
            if (b.ReadMinutes > 0) meta.Add(b.ReadMinutes.ToString(CultureInfo.InvariantCulture) + " minute read");
            if (meta.Any()) sb.Append("<p class=\"meta\">").Append(Enc(String.Join(" · ", meta))).Append("</p>");
            if (playable) AppendAudio(sb, shared, links, startSeconds);
            sb.Append("<div class=\"content\" id=\"read\">").Append(Content(b.Markdown)).Append("</div>");
            sb.Append("</article>");
            string description = (playable ? "Listen to or read a briefing" : "A briefing") + (String.IsNullOrWhiteSpace(shared.TopicName) ? String.Empty : " on " + shared.TopicName!.Trim()) + ", researched and written by My Morning News.";
            return Page(b.Title, description, links, sb.ToString(), true);
        }

        /// <summary>
        /// The address older shares handed out as a separate listening page. It renders the same single page as
        /// <see cref="Read"/>, so links made before the two pages were joined keep working.
        /// </summary>
        /// <param name="shared">The shared briefing.</param>
        /// <param name="links">Page addresses.</param>
        /// <param name="startSeconds">Where playback starts (a chapter link), or null for the beginning.</param>
        /// <returns>HTML document.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static string Listen(SharedBriefing shared, SharePageLinks links, double? startSeconds)
        {
            return Read(shared, links, startSeconds);
        }

        /// <summary>
        /// The page for every link that does not work: unknown, revoked, expired, deleted, or sharing turned off.
        /// It is identical for each, so a link cannot be probed.
        /// </summary>
        /// <param name="links">Page addresses.</param>
        /// <returns>HTML document.</returns>
        /// <exception cref="ArgumentNullException">links is null.</exception>
        public static string NotFound(SharePageLinks links)
        {
            ArgumentNullException.ThrowIfNull(links);
            string body = "<article><h1>This link is not available</h1><p>The briefing it pointed to may have expired or is no longer shared. Ask the person who sent it for a new link.</p></article>";
            return Page("Link not available", "This shared briefing link is not available.", links, body, false);
        }

        /// <summary>
        /// The page shown when one address sends too many requests.
        /// </summary>
        /// <param name="links">Page addresses.</param>
        /// <returns>HTML document.</returns>
        /// <exception cref="ArgumentNullException">links is null.</exception>
        public static string TooMany(SharePageLinks links)
        {
            ArgumentNullException.ThrowIfNull(links);
            string body = "<article><h1>Please wait a moment</h1><p>Too many requests came from your network. Try again in a minute.</p></article>";
            return Page("Please wait", "Too many requests.", links, body, false);
        }

        /// <summary>
        /// Render briefing Markdown for a public page: the leading title line is dropped (the page shows the title
        /// itself), raw HTML is shown as text, and every link gets rel="noopener noreferrer nofollow".
        /// </summary>
        /// <param name="markdown">Briefing Markdown; null renders empty.</param>
        /// <returns>HTML fragment.</returns>
        public static string Content(string? markdown)
        {
            string md = _LeadingTitle.Replace((markdown ?? String.Empty).Replace("\r\n", "\n"), String.Empty, 1);
            // SafeMarkdown encodes all text, so a literal <a href=" in its output can only be a link it wrote itself.
            return SafeMarkdown.ToHtml(md).Replace("<a href=\"", "<a rel=\"" + OutboundRel + "\" href=\"", StringComparison.Ordinal);
        }

        private static void AppendAudio(StringBuilder sb, SharedBriefing shared, SharePageLinks links, double? startSeconds)
        {
            double duration = shared.Audio!.DurationSeconds;
            double start = startSeconds.HasValue && startSeconds.Value > 0 && (duration <= 0 || startSeconds.Value < duration) ? Math.Floor(startSeconds.Value) : 0;
            sb.Append("<section class=\"listen\" id=\"listen\" aria-label=\"Listen\">");
            sb.Append("<audio controls preload=\"metadata\" src=\"").Append(Enc(links.AudioPath!));
            if (start > 0) sb.Append("#t=").Append(start.ToString("0", CultureInfo.InvariantCulture));
            sb.Append("\"");
            if (!String.IsNullOrWhiteSpace(shared.Audio.ContentType)) sb.Append(" type=\"").Append(Enc(shared.Audio.ContentType)).Append("\"");
            sb.Append(">Your browser cannot play this audio here. <a href=\"").Append(Enc(links.AudioPath!)).Append("\">Open the audio file</a>.</audio>");
            if (shared.Chapters.Count > 1)
            {
                sb.Append("<nav class=\"chapters\" aria-label=\"Chapters\"><h2>Chapters</h2><ol>");
                foreach (MediaChapter c in shared.Chapters)
                {
                    double at = Math.Floor(Math.Max(0, c.StartSeconds));
                    sb.Append("<li><a href=\"").Append(Enc(links.ReadPath)).Append("?t=").Append(at.ToString("0", CultureInfo.InvariantCulture)).Append("#listen\"");
                    if (start > 0 && Math.Abs(at - start) < 0.5) sb.Append(" aria-current=\"true\"");
                    sb.Append("><span>").Append(Enc(c.Title)).Append("</span><span class=\"time\">").Append(Enc(Clock(at))).Append("</span></a></li>");
                }

                sb.Append("</ol></nav>");
            }

            sb.Append("</section>");
        }

        private static void AppendHeading(StringBuilder sb, SharedBriefing shared, string kicker)
        {
            Briefing b = shared.Briefing;
            sb.Append("<p class=\"kicker\">").Append(Enc(kicker));
            if (!String.IsNullOrWhiteSpace(shared.TopicName)) sb.Append(" &middot; ").Append(Enc(shared.TopicName!.Trim()));
            sb.Append("</p>");
            sb.Append("<h1>").Append(Enc(b.Title)).Append("</h1>");
            DateTime when = b.ComposedUtc ?? b.CreatedUtc;
            sb.Append("<p class=\"meta\"><time datetime=\"").Append(when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("\">")
                .Append(Enc(when.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture))).Append("</time>");
            if (!String.IsNullOrWhiteSpace(shared.OwnerName)) sb.Append(" &middot; Shared by ").Append(Enc(shared.OwnerName!.Trim()));
            sb.Append("</p>");
        }

        private static string Page(string title, string description, SharePageLinks links, string main, bool withPitch)
        {
            string cta = SafeMarkdown.IsSafeUrl(links.SignupUrl) ? links.SignupUrl : links.SiteUrl;
            string fullTitle = (String.IsNullOrWhiteSpace(title) ? "Briefing" : title.Trim()) + " | My Morning News";
            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            sb.Append("<title>").Append(Enc(fullTitle)).Append("</title>\n");
            sb.Append("<meta name=\"description\" content=\"").Append(Enc(description)).Append("\">\n");
            sb.Append("<meta name=\"robots\" content=\"noindex, nofollow\">\n<meta name=\"referrer\" content=\"no-referrer\">\n<meta name=\"color-scheme\" content=\"light dark\">\n");
            sb.Append("<meta property=\"og:type\" content=\"article\">\n<meta property=\"og:site_name\" content=\"My Morning News\">\n");
            sb.Append("<meta property=\"og:title\" content=\"").Append(Enc(fullTitle)).Append("\">\n");
            sb.Append("<meta property=\"og:description\" content=\"").Append(Enc(description)).Append("\">\n");
            if (SafeMarkdown.IsSafeUrl(links.LogoUrl))
            {
                sb.Append("<meta property=\"og:image\" content=\"").Append(Enc(links.LogoUrl)).Append("\">\n<meta property=\"og:image:alt\" content=\"My Morning News\">\n");
                sb.Append("<meta name=\"twitter:image\" content=\"").Append(Enc(links.LogoUrl)).Append("\">\n");
            }

            sb.Append("<meta name=\"twitter:card\" content=\"summary\">\n");
            sb.Append("<meta name=\"twitter:title\" content=\"").Append(Enc(fullTitle)).Append("\">\n");
            sb.Append("<meta name=\"twitter:description\" content=\"").Append(Enc(description)).Append("\">\n");
            sb.Append("<style>").Append(Css).Append("</style>\n</head>\n<body>\n");
            sb.Append("<a class=\"skip\" href=\"#main\">Skip to content</a>\n");
            sb.Append("<header class=\"top\"><div class=\"wrap\"><a class=\"brand\" href=\"").Append(Enc(links.SiteUrl)).Append("\" rel=\"noopener noreferrer\"><img src=\"").Append(Enc(links.LogoPath))
                .Append("\" alt=\"My Morning News\" width=\"108\" height=\"36\"></a>");
            sb.Append("<a class=\"btn small\" href=\"").Append(Enc(cta)).Append("\" rel=\"noopener noreferrer\">").Append(links.SignupOpen ? "Get your own" : "Learn more").Append("</a></div></header>\n");
            sb.Append("<main id=\"main\"><div class=\"wrap\">\n");
            if (withPitch)
            {
                sb.Append("<aside class=\"pitch\" aria-label=\"About this page\"><p>This briefing was researched and written by My Morning News for one reader's own topic. ")
                    .Append("<a href=\"").Append(Enc(cta)).Append("\" rel=\"noopener noreferrer\">Get your own, on the topics you choose</a>.</p></aside>\n");
            }

            sb.Append(main).Append('\n');
            sb.Append("<section class=\"cta\" aria-labelledby=\"cta-title\"><h2 id=\"cta-title\">Get a briefing like this on your own topics</h2>");
            sb.Append("<p>My Morning News is a research assistant. You tell it what you want to follow, and it does the reading for you.</p><ul>");
            sb.Append("<li><strong>Research on your schedule.</strong> Choose your topics and when they run. Each run searches, reads, and writes you a briefing.</li>");
            sb.Append("<li><strong>Evidence you can check.</strong> Findings are tied to the sources they came from, so you can open them and judge for yourself.</li>");
            sb.Append("<li><strong>Audio you can listen to.</strong> A briefing can be read aloud, so you can listen instead of reading.</li></ul>");
            sb.Append("<a class=\"btn\" href=\"").Append(Enc(cta)).Append("\" rel=\"noopener noreferrer\">").Append(links.SignupOpen ? "Create your account" : "Learn more at mymorningnews.ai").Append("</a>");
            sb.Append("</section>\n</div></main>\n");
            sb.Append("<footer class=\"end\"><div class=\"wrap\"><a href=\"").Append(Enc(links.SiteUrl)).Append("\" rel=\"noopener noreferrer\">My Morning News</a> &middot; ")
                .Append(BriefingHtmlRenderer.Copyright).Append("</div></footer>\n</body>\n</html>\n");
            return sb.ToString();
        }

        private static string Clock(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds)));
            return t.TotalHours >= 1
                ? ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + t.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture)
                : t.Minutes.ToString(CultureInfo.InvariantCulture) + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string Enc(string? s)
        {
            return WebUtility.HtmlEncode(s ?? String.Empty);
        }
    }
}
