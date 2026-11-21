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
    /// Server-rendered public pages for email links (plan section 23.5): feedback, thanks, unsubscribe, and a uniform
    /// invalid-link page. Self-contained HTML with inline CSS, light and dark, usable at 360 px, no JavaScript.
    /// Thread safety: stateless.
    /// </summary>
    public static class PublicPages
    {
        private const string Css =
            ":root{color-scheme:light dark;--bg:#f3f4f6;--card:#ffffff;--fg:#1c1f24;--muted:#6b7280;--line:#d1d5db;--accent:#b4531f}" +
            "@media (prefers-color-scheme:dark){:root{--bg:#14161a;--card:#1d2026;--fg:#e8eaed;--muted:#a2a9b3;--line:#3a4049;--accent:#f0915c}}" +
            "html,body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.5 -apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif}" +
            "main{max-width:560px;margin:0 auto;padding:24px 16px}.card{background:var(--card);border-radius:10px;padding:22px}" +
            "img.logo{width:150px;max-width:55%;height:auto;margin:0 0 16px}h1{font-size:21px;line-height:1.3;margin:0 0 8px}p{margin:0 0 12px}.muted{color:var(--muted);font-size:14px}" +
            "fieldset{border:0;margin:14px 0;padding:0}legend{font-weight:600;margin-bottom:8px}.opts{display:flex;flex-wrap:wrap;gap:8px}" +
            ".opts label{border:1px solid var(--line);border-radius:18px;padding:6px 12px;cursor:pointer}.opts input{margin-right:6px}" +
            ".opts label:has(input:checked){border-color:var(--accent);outline:1px solid var(--accent)}.confirm{border-left:3px solid var(--accent);padding-left:10px}" +
            "textarea{width:100%;box-sizing:border-box;min-height:110px;border:1px solid var(--line);border-radius:8px;padding:10px;background:transparent;color:inherit;font:inherit}" +
            "button{margin-top:14px;padding:10px 18px;border:0;border-radius:6px;background:var(--accent);color:#fff;font:600 15px/1 inherit;cursor:pointer}a{color:var(--accent)}" +
            "footer{margin-top:18px;font-size:12px;color:var(--muted);text-align:center}";

        /// <summary>
        /// The feedback form with the chosen action preselected. Opening an email rating link never records the rating by
        /// itself (mail security scanners open links automatically), so the page asks the reader to confirm it with one
        /// clearly labeled button and says why.
        /// </summary>
        /// <param name="logoUrl">Logo URL.</param>
        /// <param name="postUrl">Form action URL.</param>
        /// <param name="itemTitle">Item title.</param>
        /// <param name="itemSummary">Item summary, or null.</param>
        /// <param name="actions">Allowed actions.</param>
        /// <param name="selected">Preselected action, or null.</param>
        /// <param name="openUrl">Link to open the item in the web app, or null.</param>
        /// <param name="recorded">Whether the selected rating was already recorded on open.</param>
        /// <returns>HTML.</returns>
        public static string Feedback(string logoUrl, string postUrl, string itemTitle, string? itemSummary, IReadOnlyList<string> actions, string? selected, string? openUrl, bool recorded)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<h1>").Append(Enc(itemTitle)).Append("</h1>");
            if (!String.IsNullOrWhiteSpace(itemSummary)) sb.Append("<p class=\"muted\">").Append(Enc(itemSummary)).Append("</p>");
            string? chosen = selected != null && actions.Contains(selected, StringComparer.Ordinal) ? selected : null;
            if (recorded) sb.Append("<p><strong>Thanks, your rating is saved.</strong> You can change it or add a reason below.</p>");
            else if (chosen != null) sb.Append("<p class=\"confirm\"><strong>Confirm your rating: “").Append(Enc(EmailActions.Label(chosen))).Append("”.</strong> It is not saved until you press the button below.</p>");
            else sb.Append("<p class=\"confirm\"><strong>Choose a rating</strong>, then press the button below to save it.</p>");
            sb.Append("<form method=\"post\" action=\"").Append(Enc(postUrl)).Append("\">");
            sb.Append("<fieldset><legend>Your rating</legend><div class=\"opts\">");
            foreach (string a in actions)
            {
                sb.Append("<label><input type=\"radio\" name=\"a\" value=\"").Append(Enc(a)).Append('"');
                if (String.Equals(a, chosen, StringComparison.Ordinal)) sb.Append(" checked");
                sb.Append('>').Append(Enc(EmailActions.Label(a))).Append("</label>");
            }

            sb.Append("</div></fieldset>");
            sb.Append("<label for=\"reason\"><strong>Why?</strong> <span class=\"muted\">(optional)</span></label>");
            sb.Append("<textarea id=\"reason\" name=\"reason\" maxlength=\"2000\" placeholder=\"What made this useful or not? It helps tune your future briefings.\"></textarea>");
            string button = recorded ? "Update my rating" : (chosen != null ? "Save “" + Enc(EmailActions.Label(chosen)) + "”" : "Save my rating");
            sb.Append("<button type=\"submit\">").Append(button).Append("</button></form>");
            if (!recorded) sb.Append("<p class=\"muted\" style=\"margin-top:12px\">We ask you to confirm because email security scanners open links on their own, and a click from a scanner should not count as your rating.</p>");
            if (SafeMarkdown.IsSafeUrl(openUrl)) sb.Append("<p class=\"muted\" style=\"margin-top:16px\"><a href=\"").Append(Enc(openUrl!)).Append("\">Open in My Morning News</a></p>");
            return Page("Your feedback", logoUrl, sb.ToString());
        }

        /// <summary>
        /// The thank-you page after a submission.
        /// </summary>
        /// <param name="logoUrl">Logo URL.</param>
        /// <param name="changeUrl">Link to change the answer.</param>
        /// <param name="action">Recorded action, or null.</param>
        /// <param name="hadReason">Whether a reason was written.</param>
        /// <param name="openUrl">Link to the run or item, or null.</param>
        /// <returns>HTML.</returns>
        public static string Thanks(string logoUrl, string changeUrl, string? action, bool hadReason, string? openUrl)
        {
            StringBuilder sb = new StringBuilder("<h1>Thank you</h1>");
            sb.Append("<p>We saved ").Append(action == null ? "your feedback" : "“" + Enc(EmailActions.Label(action)) + "”").Append(".</p>");
            if (hadReason) sb.Append("<p>Your reason will help tune future runs.</p>");
            sb.Append("<p><a href=\"").Append(Enc(changeUrl)).Append("\">Change my answer</a>");
            if (SafeMarkdown.IsSafeUrl(openUrl)) sb.Append(" &middot; <a href=\"").Append(Enc(openUrl!)).Append("\">Open in My Morning News</a>");
            sb.Append("</p>");
            return Page("Thank you", logoUrl, sb.ToString());
        }

        /// <summary>
        /// The unsubscribe confirmation form, or the done page with an undo link.
        /// </summary>
        /// <param name="logoUrl">Logo URL.</param>
        /// <param name="postUrl">Form action URL.</param>
        /// <param name="what">What is being stopped (for example "run emails for Heat pumps").</param>
        /// <param name="done">Whether the change is already made.</param>
        /// <param name="resubscribed">Whether the change was undone.</param>
        /// <returns>HTML.</returns>
        public static string Unsubscribe(string logoUrl, string postUrl, string what, bool done, bool resubscribed)
        {
            StringBuilder sb = new StringBuilder();
            if (resubscribed)
            {
                sb.Append("<h1>You are subscribed again</h1><p>You will get ").Append(Enc(what)).Append(" again.</p>");
                sb.Append("<form method=\"post\" action=\"").Append(Enc(postUrl)).Append("\"><input type=\"hidden\" name=\"op\" value=\"unsubscribe\"><button type=\"submit\">Unsubscribe</button></form>");
            }
            else if (done)
            {
                sb.Append("<h1>You are unsubscribed</h1><p>You will no longer get ").Append(Enc(what)).Append(".</p>");
                sb.Append("<form method=\"post\" action=\"").Append(Enc(postUrl)).Append("\"><input type=\"hidden\" name=\"op\" value=\"undo\"><button type=\"submit\">Undo</button></form>");
            }
            else
            {
                sb.Append("<h1>Unsubscribe?</h1><p>Stop getting ").Append(Enc(what)).Append(".</p>");
                sb.Append("<form method=\"post\" action=\"").Append(Enc(postUrl)).Append("\"><input type=\"hidden\" name=\"op\" value=\"unsubscribe\"><button type=\"submit\">Unsubscribe</button></form>");
            }

            return Page("Email preferences", logoUrl, sb.ToString());
        }

        /// <summary>
        /// The page for unknown, expired, or revoked links (identical for each, so links cannot be probed).
        /// </summary>
        /// <param name="logoUrl">Logo URL.</param>
        /// <param name="appUrl">Web app URL.</param>
        /// <returns>HTML.</returns>
        public static string Invalid(string logoUrl, string appUrl)
        {
            return Page("Link not available", logoUrl, "<h1>This link is no longer available</h1><p>It may have expired. Open My Morning News to give feedback or change your email settings.</p>" +
                (SafeMarkdown.IsSafeUrl(appUrl) ? "<p><a href=\"" + Enc(appUrl) + "\">Open My Morning News</a></p>" : String.Empty));
        }

        /// <summary>
        /// The page shown when a client sends too many requests.
        /// </summary>
        /// <param name="logoUrl">Logo URL.</param>
        /// <returns>HTML.</returns>
        public static string TooMany(string logoUrl)
        {
            return Page("Please wait", logoUrl, "<h1>Please wait a moment</h1><p>Too many requests came from your network. Try again in a minute.</p>");
        }

        /// <summary>
        /// Parse an application/x-www-form-urlencoded body.
        /// </summary>
        /// <param name="body">Body.</param>
        /// <returns>Fields (last value wins).</returns>
        public static Dictionary<string, string> ParseForm(string? body)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in (body ?? String.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                string k = WebUtility.UrlDecode(eq < 0 ? pair : pair.Substring(0, eq)) ?? String.Empty;
                string v = eq < 0 ? String.Empty : WebUtility.UrlDecode(pair.Substring(eq + 1)) ?? String.Empty;
                if (k.Length > 0) fields[k] = v;
            }

            return fields;
        }

        private static string Page(string title, string logoUrl, string content)
        {
            string body = "<main><div class=\"card\">" + (SafeMarkdown.IsSafeUrl(logoUrl) ? "<img class=\"logo\" src=\"" + Enc(logoUrl) + "\" alt=\"My Morning News\">" : String.Empty) + content +
                "</div><footer>My Morning News &middot; " + Enc(BriefingHtmlRenderer.Copyright) + "</footer></main>";
            using (Html builder = new Html())
            {
                return builder.Page()
                    .WithLanguage("en")
                    .WithTitle(title + " | My Morning News")
                    .WithHead(h => h.AddRawElement("<meta name=\"color-scheme\" content=\"light dark\">").AddRawElement("<meta name=\"robots\" content=\"noindex, nofollow\">").WithStyle(Css))
                    .WithBody(b => b.AddRaw(body))
                    .ToString();
            }
        }

        private static string Enc(string? s)
        {
            return WebUtility.HtmlEncode(s ?? String.Empty);
        }
    }
}
