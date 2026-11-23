namespace MyMorningNews.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using Voltaic.Mcp;

    /// <summary>
    /// Public sharing tools: share a briefing through links that need no sign-in, list a briefing's shares, and revoke one.
    /// </summary>
    public static class ShareTools
    {
        /// <summary>
        /// Shortest share life in days.
        /// </summary>
        public const int MinExpiryDays = 1;

        /// <summary>
        /// Longest share life in days the tool accepts. The server may be configured lower and then refuses the value.
        /// </summary>
        public const int MaxExpiryDays = 14;

        /// <summary>
        /// Share life in days when the caller does not say.
        /// </summary>
        public const int DefaultExpiryDays = 7;

        /// <summary>
        /// Build the tools.
        /// </summary>
        /// <returns>Tools.</returns>
        public static List<McpTool> Build()
        {
            List<McpTool> tools = new List<McpTool>();
            Dictionary<string, object> created = Schema.Object(Schema.Props(
                "share", ShareSchema,
                "readUrl", Schema.String("Public reading page. Anyone with the link can open it with no sign-in."),
                "listenUrl", Schema.String("Public listening page. Anyone with the link can open it with no sign-in."),
                "audioReady", Schema.Boolean("Whether the briefing has finished audio now. When false the listening page says the audio is not ready.")),
                new[] { "share", "readUrl", "listenUrl" }, true);

            tools.Add(new McpTool("share_briefing", "Share a briefing with a public link",
                "Create public links for one of your ready briefings: a reading page and a listening page that anyone with the link can open with no sign-in. " +
                "The links are returned once and cannot be read back later, so give them to the user now. Every share expires: expiresInDays is 1 to 14 (default 7), and a value outside that range is refused. " +
                "Only share when the user asks to.",
                Schema.Object(Schema.Props(
                    "briefingId", Schema.String("Briefing ID (brf_...).", 64),
                    "expiresInDays", Schema.Integer("Days until the links stop working. 1 to 14. Default 7.", MinExpiryDays, MaxExpiryDays),
                    "showOwnerName", Schema.Boolean("Show your display name on the public pages. Default false. Your email address is never shown.")), new[] { "briefingId" }),
                created, false, false,
                async (a, c, t) =>
                {
                    BriefingShareCreateRequest req = new BriefingShareCreateRequest
                    {
                        ExpiresInDays = a.Integer("expiresInDays", DefaultExpiryDays, MinExpiryDays, MaxExpiryDays),
                        ShowOwnerName = a.Flag("showOwnerName") == true
                    };
                    BriefingShareCreated s = await c.Briefings.ShareAsync(a.RequiredText("briefingId", 64), req, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        { "share", Share(s.Share) }, { "readUrl", s.ReadUrl }, { "listenUrl", s.ListenUrl }, { "audioReady", s.AudioReady }
                    };
                }));

            tools.Add(new McpTool("list_briefing_shares", "List a briefing's public links",
                "List the public share links of one briefing, newest first: when each expires (1 to 14 days after it was made), whether it was revoked, and how often it was opened. The links themselves are shown only when a share is created.",
                Schema.Object(Schema.Props(
                    "briefingId", Schema.String("Briefing ID (brf_...).", 64),
                    "pageSize", Schema.Integer("Shares per page. Default 20.", 1, 100),
                    "pageNumber", Schema.Integer("Page (1-based). Default 1.", 1, 10000)), new[] { "briefingId" }),
                Schema.Page(ShareSchema), true, false,
                async (a, c, t) =>
                {
                    ListOptions o = new ListOptions { PageSize = a.Integer("pageSize", 20, 1, 100), PageNumber = a.Integer("pageNumber", 1, 1, 10000) };
                    return Shapes.Page(await c.Briefings.ListSharesAsync(a.RequiredText("briefingId", 64), o, t).ConfigureAwait(false), Share);
                }));

            tools.Add(new McpTool("revoke_briefing_share", "Stop sharing a briefing link",
                "Revoke one public share so its reading and listening links stop working at once. This cannot be undone; make a new share to share again.",
                Schema.Object(Schema.Props(
                    "briefingId", Schema.String("Briefing ID (brf_...).", 64),
                    "shareId", Schema.String("Share ID (shr_...), from list_briefing_shares or share_briefing.", 64)), new[] { "briefingId", "shareId" }),
                Schema.Object(Schema.Props("revoked", Schema.Boolean("True when the share is revoked."), "shareId", Schema.String("Share ID.")), new[] { "revoked", "shareId" }),
                false, true,
                async (a, c, t) =>
                {
                    string shareId = a.RequiredText("shareId", 64);
                    await c.Briefings.RevokeShareAsync(a.RequiredText("briefingId", 64), shareId, false, t).ConfigureAwait(false);
                    return new Dictionary<string, object?>(StringComparer.Ordinal) { { "revoked", true }, { "shareId", shareId } };
                }));

            return tools;
        }

        /// <summary>
        /// Shape a share. The stored hash is left out.
        /// </summary>
        /// <param name="s">Share.</param>
        /// <returns>Projection.</returns>
        /// <exception cref="ArgumentNullException">Share is null.</exception>
        public static Dictionary<string, object?> Share(BriefingShare s)
        {
            ArgumentNullException.ThrowIfNull(s);
            DateTime now = DateTime.UtcNow;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "id", s.Id }, { "briefingId", s.BriefingId }, { "status", s.RevokedUtc.HasValue ? "Revoked" : s.ExpiresUtc <= now ? "Expired" : "Live" },
                { "expiresUtc", s.ExpiresUtc.ToUniversalTime().ToString("o") }, { "revokedUtc", s.RevokedUtc?.ToUniversalTime().ToString("o") },
                { "showOwnerName", s.ShowOwnerName }, { "viewCount", s.ViewCount }, { "lastViewedUtc", s.LastViewedUtc?.ToUniversalTime().ToString("o") },
                { "createdUtc", s.CreatedUtc.ToUniversalTime().ToString("o") }
            };
        }

        /// <summary>
        /// Share schema.
        /// </summary>
        public static Dictionary<string, object> ShareSchema
        {
            get
            {
                return Schema.Object(Schema.Props(
                    "id", Schema.String("Share ID (shr_...)."), "briefingId", Schema.String("Briefing."), "status", Schema.Choice("Whether the links work.", "Live", "Expired", "Revoked"),
                    "expiresUtc", Schema.String("When the links stop working (UTC)."), "revokedUtc", Schema.NullableString("Revoked (UTC)."),
                    "showOwnerName", Schema.Boolean("The public pages show the sharing user's name."), "viewCount", Schema.Integer("Times a public page was opened.", 0, long.MaxValue),
                    "lastViewedUtc", Schema.NullableString("Last opened (UTC)."), "createdUtc", Schema.String("Created (UTC).")),
                    new[] { "id", "briefingId", "status", "expiresUtc" }, true);
            }
        }
    }
}
