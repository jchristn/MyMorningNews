namespace MyMorningNews.Core.Email
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using HtmlPageBuilder;
    using MyMorningNews.Core.Briefings;

    /// <summary>
    /// Builds an email as HTML and plain text side by side: inline styles, table layout up to 600 px, readable at 360 px,
    /// light and dark (color-scheme), a hidden preheader, and every value encoded exactly once. Links allow http, https,
    /// and mailto only. Not thread safe; use one builder per email.
    /// </summary>
    public class EmailBuilder
    {
        private readonly StringBuilder _Html = new StringBuilder();
        private readonly StringBuilder _Text = new StringBuilder();
        private string _Preheader = String.Empty;
        private const string _Font = "-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

        /// <summary>
        /// Hidden preview text shown by mail clients after the subject.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Preheader(string? text)
        {
            _Preheader = text ?? String.Empty;
            return this;
        }

        /// <summary>
        /// Header with the logo, a title, an optional subtitle, and an optional button.
        /// </summary>
        /// <param name="logoUrl">Logo URL, or null.</param>
        /// <param name="title">Title.</param>
        /// <param name="subtitle">Subtitle, or null.</param>
        /// <param name="button">Button, or null.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Header(string? logoUrl, string title, string? subtitle, EmailLink? button)
        {
            if (SafeMarkdown.IsSafeUrl(logoUrl)) _Html.Append("<tr><td style=\"padding:0 0 16px\"><img src=\"").Append(Attr(logoUrl!)).Append("\" alt=\"My Morning News\" width=\"160\" style=\"display:block;width:160px;max-width:60%;height:auto;border:0\"></td></tr>");
            _Html.Append("<tr><td style=\"padding:0 0 4px;font:700 24px/1.25 ").Append(_Font).Append("\">").Append(Enc(title)).Append("</td></tr>");
            if (!String.IsNullOrWhiteSpace(subtitle)) _Html.Append("<tr><td style=\"padding:0 0 12px;font:14px/1.4 ").Append(_Font).Append(";color:#6b7280\">").Append(Enc(subtitle)).Append("</td></tr>");
            _Text.Append(title).Append('\n');
            if (!String.IsNullOrWhiteSpace(subtitle)) _Text.Append(subtitle).Append('\n');
            if (button != null) Button(button);
            _Text.Append('\n');
            return this;
        }

        /// <summary>
        /// A primary button.
        /// </summary>
        /// <param name="link">Link.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Button(EmailLink link)
        {
            ArgumentNullException.ThrowIfNull(link);
            if (!SafeMarkdown.IsSafeUrl(link.Url)) return this;
            _Html.Append("<tr><td style=\"padding:4px 0 16px\"><a href=\"").Append(Attr(link.Url)).Append("\" style=\"display:inline-block;padding:10px 18px;border-radius:6px;background:#2563eb;color:#ffffff;text-decoration:none;font:600 14px/1 ")
                .Append(_Font).Append("\">").Append(Enc(link.Label)).Append("</a></td></tr>");
            _Text.Append(link.Label).Append(": ").Append(link.Url).Append('\n');
            return this;
        }

        /// <summary>
        /// A plain paragraph.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Paragraph(string? text)
        {
            if (String.IsNullOrWhiteSpace(text)) return this;
            _Html.Append("<tr><td style=\"padding:0 0 14px;font:16px/1.55 ").Append(_Font).Append("\">").Append(Enc(text).Replace("\n", "<br>")).Append("</td></tr>");
            _Text.Append(text.Trim()).Append("\n\n");
            return this;
        }

        /// <summary>
        /// Markdown rendered with the safe subset (headings shifted down two levels).
        /// </summary>
        /// <param name="markdown">Markdown.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Markdown(string? markdown)
        {
            if (String.IsNullOrWhiteSpace(markdown)) return this;
            _Html.Append("<tr><td style=\"padding:0 0 14px;font:16px/1.55 ").Append(_Font).Append("\">").Append(SafeMarkdown.ToHtml(markdown, 2)).Append("</td></tr>");
            _Text.Append(SafeMarkdown.ToText(markdown)).Append("\n\n");
            return this;
        }

        /// <summary>
        /// A section heading.
        /// </summary>
        /// <param name="text">Heading.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Section(string text)
        {
            _Html.Append("<tr><td style=\"padding:20px 0 8px;border-top:1px solid #e5e7eb;font:700 18px/1.3 ").Append(_Font).Append("\">").Append(Enc(text)).Append("</td></tr>");
            _Text.Append(new string('=', Math.Min(60, text.Length))).Append('\n').Append(text).Append("\n\n");
            return this;
        }

        /// <summary>
        /// An item card: badge, linked title, lines of text, source chips, and an action row.
        /// </summary>
        /// <param name="badge">Badge such as "Must read 87", or null.</param>
        /// <param name="title">Title.</param>
        /// <param name="titleUrl">Title link, or null.</param>
        /// <param name="lines">Text lines.</param>
        /// <param name="chips">Source chips.</param>
        /// <param name="actions">Action links (feedback).</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Item(string? badge, string title, string? titleUrl, IEnumerable<string> lines, IEnumerable<EmailLink> chips, IEnumerable<EmailLink> actions)
        {
            List<string> body = (lines ?? Enumerable.Empty<string>()).Where(l => !String.IsNullOrWhiteSpace(l)).ToList();
            List<EmailLink> src = (chips ?? Enumerable.Empty<EmailLink>()).Where(c => SafeMarkdown.IsSafeUrl(c.Url)).ToList();
            List<EmailLink> acts = (actions ?? Enumerable.Empty<EmailLink>()).Where(a => SafeMarkdown.IsSafeUrl(a.Url)).ToList();
            _Html.Append("<tr><td style=\"padding:0 0 18px\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-left:3px solid #2563eb\"><tr><td style=\"padding:2px 0 2px 12px;font:15px/1.5 ").Append(_Font).Append("\">");
            if (!String.IsNullOrWhiteSpace(badge)) _Html.Append("<div class=\"mmn-accent\" style=\"font:600 12px/1.4 ").Append(_Font).Append(";color:#2563eb;text-transform:uppercase;letter-spacing:.03em\">").Append(Enc(badge)).Append("</div>");
            _Html.Append("<div style=\"font:600 16px/1.35 ").Append(_Font).Append(";margin:2px 0 4px\">");
            if (SafeMarkdown.IsSafeUrl(titleUrl)) _Html.Append("<a href=\"").Append(Attr(titleUrl!)).Append("\" style=\"color:inherit;text-decoration:underline\">").Append(Enc(title)).Append("</a>");
            else _Html.Append(Enc(title));
            _Html.Append("</div>");
            foreach (string l in body) _Html.Append("<div style=\"margin:0 0 4px\">").Append(Enc(l)).Append("</div>");
            if (src.Count > 0)
            {
                _Html.Append("<div style=\"margin:6px 0 2px;font:12px/1.6 ").Append(_Font).Append(";color:#6b7280\">");
                _Html.Append(String.Join(" &middot; ", src.Select(c => "<a href=\"" + Attr(c.Url) + "\" style=\"color:#6b7280\">" + Enc(c.Label) + "</a>")));
                _Html.Append("</div>");
            }

            if (acts.Count > 0)
            {
                _Html.Append("<div style=\"margin:8px 0 0\">");
                foreach (EmailLink a in acts)
                    _Html.Append("<a href=\"").Append(Attr(a.Url)).Append("\" style=\"display:inline-block;margin:0 6px 6px 0;padding:5px 10px;border:1px solid #d1d5db;border-radius:14px;color:#374151;text-decoration:none;font:13px/1.2 ")
                        .Append(_Font).Append("\">").Append(Enc(a.Label)).Append("</a>");
                _Html.Append("</div>");
            }

            _Html.Append("</td></tr></table></td></tr>");
            _Text.Append(String.IsNullOrWhiteSpace(badge) ? String.Empty : "[" + badge + "] ").Append(title).Append('\n');
            if (SafeMarkdown.IsSafeUrl(titleUrl)) _Text.Append(titleUrl).Append('\n');
            foreach (string l in body) _Text.Append(l).Append('\n');
            foreach (EmailLink c in src) _Text.Append("Source: ").Append(c.Label).Append(' ').Append(c.Url).Append('\n');
            foreach (EmailLink a in acts) _Text.Append("  ").Append(a.Label).Append(": ").Append(a.Url).Append('\n');
            _Text.Append('\n');
            return this;
        }

        /// <summary>
        /// The footer: links and a note.
        /// </summary>
        /// <param name="links">Links.</param>
        /// <param name="note">Note, or null.</param>
        /// <returns>This builder.</returns>
        public EmailBuilder Footer(IEnumerable<EmailLink> links, string? note)
        {
            List<EmailLink> list = (links ?? Enumerable.Empty<EmailLink>()).Where(l => SafeMarkdown.IsSafeUrl(l.Url)).ToList();
            _Html.Append("<tr><td style=\"padding:20px 0 0;border-top:1px solid #e5e7eb;font:12px/1.6 ").Append(_Font).Append(";color:#6b7280\">");
            if (list.Count > 0) _Html.Append(String.Join(" &middot; ", list.Select(l => "<a href=\"" + Attr(l.Url) + "\" style=\"color:#6b7280\">" + Enc(l.Label) + "</a>"))).Append("<br>");
            if (!String.IsNullOrWhiteSpace(note)) _Html.Append(Enc(note)).Append("<br>");
            _Html.Append(Enc(BriefingHtmlRenderer.Copyright)).Append("</td></tr>");
            _Text.Append("--\n");
            foreach (EmailLink l in list) _Text.Append(l.Label).Append(": ").Append(l.Url).Append('\n');
            if (!String.IsNullOrWhiteSpace(note)) _Text.Append(note).Append('\n');
            _Text.Append(BriefingHtmlRenderer.Copyright).Append('\n');
            return this;
        }

        /// <summary>
        /// A two-column table of labeled values, for summaries. Empty input adds nothing.
        /// </summary>
        /// <param name="metrics">Rows.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">metrics is null.</exception>
        public EmailBuilder Metrics(IEnumerable<EmailMetric> metrics)
        {
            ArgumentNullException.ThrowIfNull(metrics);
            List<EmailMetric> rows = metrics.Where(m => m != null).ToList();
            if (rows.Count == 0) return this;
            _Html.Append("<tr><td style=\"padding:0 0 14px\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse\">");
            foreach (EmailMetric m in rows)
            {
                _Html.Append("<tr><td style=\"padding:6px 8px 6px 0;border-bottom:1px solid #e5e7eb;font:15px/1.4 ").Append(_Font).Append("\">").Append(Enc(m.Label)).Append("</td>")
                    .Append("<td align=\"right\" style=\"padding:6px 0;border-bottom:1px solid #e5e7eb;font:700 15px/1.4 ").Append(_Font).Append(";white-space:nowrap\">").Append(Enc(m.Value));
                if (!String.IsNullOrWhiteSpace(m.Note)) _Html.Append(" <span style=\"font-weight:400;font-size:13px;color:#64748b\">").Append(Enc(m.Note)).Append("</span>");
                _Html.Append("</td></tr>");
                _Text.Append(m.Label).Append(": ").Append(m.Value);
                if (!String.IsNullOrWhiteSpace(m.Note)) _Text.Append(" (").Append(m.Note).Append(')');
                _Text.Append('\n');
            }

            _Html.Append("</table></td></tr>");
            _Text.Append('\n');
            return this;
        }

        /// <summary>
        /// The full HTML document.
        /// </summary>
        /// <param name="title">Document title (usually the subject).</param>
        /// <returns>HTML.</returns>
        public string Html(string title)
        {
            string css = ":root{color-scheme:light dark;supported-color-schemes:light dark}body{margin:0;padding:0}" +
                "@media (prefers-color-scheme:dark){.mmn-bg{background:#0f172a !important}.mmn-card{background:#111c33 !important;color:#e2e8f0 !important}.mmn-accent{color:#60a5fa !important}}" +
                "@media (max-width:420px){.mmn-card{padding:16px !important}}";
            string inner = "<div style=\"display:none;max-height:0;overflow:hidden;opacity:0\">" + Enc(_Preheader) + "</div>" +
                "<table role=\"presentation\" class=\"mmn-bg\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f8fafc\"><tr><td align=\"center\" style=\"padding:16px 8px\">" +
                "<table role=\"presentation\" class=\"mmn-card\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:600px;background:#ffffff;color:#0f172a;border-radius:8px;padding:24px\">" +
                _Html + "</table></td></tr></table>";
            using (Html builder = new Html())
            {
                return builder.Page()
                    .WithLanguage("en")
                    .WithTitle(String.IsNullOrWhiteSpace(title) ? "My Morning News" : title)
                    .WithHead(h => h.AddRawElement("<meta name=\"color-scheme\" content=\"light dark\">").AddRawElement("<meta name=\"supported-color-schemes\" content=\"light dark\">").WithStyle(css))
                    .WithBody(b => b.AddRaw(inner))
                    .ToString();
            }
        }

        /// <summary>
        /// The plain-text rendering.
        /// </summary>
        /// <returns>Text.</returns>
        public string Text()
        {
            return _Text.ToString().TrimEnd() + "\n";
        }

        private static string Enc(string? s)
        {
            return WebUtility.HtmlEncode(s ?? String.Empty);
        }

        private static string Attr(string s)
        {
            return WebUtility.HtmlEncode(s);
        }
    }
}
