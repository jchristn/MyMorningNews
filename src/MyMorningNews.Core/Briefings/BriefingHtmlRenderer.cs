namespace MyMorningNews.Core.Briefings
{
    using System;
    using HtmlPageBuilder;

    /// <summary>
    /// Renders a briefing's Markdown as a self-contained HTML report (inline styles, light and dark, readable at 360 px),
    /// with the same look as the email. Thread safety: stateless.
    /// </summary>
    public static class BriefingHtmlRenderer
    {
        /// <summary>
        /// Copyright notice in every report footer.
        /// </summary>
        public const string Copyright = "© 2026 Lattice AI LLC";

        private const string Css =
            ":root{color-scheme:light dark;--bg:#ffffff;--fg:#0f172a;--muted:#475569;--rule:#e2e8f0;--accent:#2563eb;--quote:#f1f5f9}" +
            "@media (prefers-color-scheme:dark){:root{--bg:#0f172a;--fg:#e2e8f0;--muted:#94a3b8;--rule:#26344f;--accent:#60a5fa;--quote:#111c33}}" +
            "html{background:var(--bg)}body{margin:0;background:var(--bg);color:var(--fg);font:17px/1.6 Georgia,'Times New Roman',serif}" +
            "main{max-width:680px;margin:0 auto;padding:32px 16px 48px}" +
            ".meta{font:13px/1.4 -apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:var(--muted);margin:0 0 24px}" +
            "h1{font-size:30px;line-height:1.2;margin:0 0 8px}h2{font-size:22px;margin:36px 0 10px;padding-top:12px;border-top:1px solid var(--rule)}" +
            "h3{font-size:18px;margin:24px 0 8px}a{color:var(--accent)}code{font-family:Menlo,Consolas,monospace;font-size:14px}" +
            "pre{overflow-x:auto;padding:12px;background:var(--quote)}blockquote{margin:16px 0;padding:8px 16px;border-left:3px solid var(--accent);background:var(--quote)}" +
            "hr{border:0;border-top:1px solid var(--rule);margin:32px 0}li{margin:4px 0}" +
            "footer{font:12px/1.5 -apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:var(--muted);border-top:1px solid var(--rule);margin-top:40px;padding-top:12px}";

        /// <summary>
        /// Render a full HTML document.
        /// </summary>
        /// <param name="title">Document title (plain text).</param>
        /// <param name="markdown">Briefing Markdown, including its # title.</param>
        /// <param name="meta">A plain-text line under the title (date, reading time), or null.</param>
        /// <returns>HTML document.</returns>
        public static string Render(string title, string? markdown, string? meta)
        {
            string body = SafeMarkdown.ToHtml(markdown);
            if (!String.IsNullOrWhiteSpace(meta))
            {
                int h1 = body.IndexOf("</h1>", StringComparison.Ordinal);
                string metaHtml = "<p class=\"meta\">" + System.Net.WebUtility.HtmlEncode(meta) + "</p>\n";
                body = h1 >= 0 ? body.Insert(h1 + 6, metaHtml) : metaHtml + body;
            }

            using (Html builder = new Html())
            {
                return builder.Page()
                    .WithLanguage("en")
                    .WithTitle(String.IsNullOrWhiteSpace(title) ? "Briefing" : title)
                    .WithHead(head => head
                        .WithMetaDescription(String.IsNullOrWhiteSpace(meta) ? "My Morning News briefing" : meta)
                        .AddRawElement("<meta name=\"color-scheme\" content=\"light dark\">")
                        .WithStyle(Css))
                    .WithBody(b => b.AddRaw("<main>\n" + body + "<footer>My Morning News. " + Copyright + "</footer>\n</main>"))
                    .ToString();
            }
        }
    }
}
