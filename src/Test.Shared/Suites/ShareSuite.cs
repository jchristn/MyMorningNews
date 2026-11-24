namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Public sharing of briefings (plan section 36): creating a share, the reading and listening pages and the audio
    /// with no credentials at all, the 1 to 14 day expiry rule, revocation, the server-wide switch, tenant isolation,
    /// and escaping of briefing content.
    /// </summary>
    public static class ShareSuite
    {
        private const string S = "Share";

        // No cookie container and no default headers: every request from this client is fully anonymous.
        private static readonly HttpClient _Bare = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Public sharing of briefings", new[]
            {
                Cases.Case(S, "Pages", "Markdown for a public page drops the title line, shows raw HTML as text, refuses unsafe links, and marks outbound links", PagesAsync, "fast"),
                Cases.Case(S, "Anonymous", "A share yields a reading page, a listening page, and ranged audio that open with no Authorization header and no cookies, and counts its openings", AnonymousAsync),
                Cases.Case(S, "Expiry", "Every share expires: 7 days by default, 1 to 14 days on request, 15 days and 0 days are refused, and an expired link gets the same 404 as an unknown one", ExpiryAsync),
                Cases.Case(S, "Revoke", "Revoking a share or deleting its briefing kills both links, and the server-wide switch turns every link and the create route off", RevokeAsync),
                Cases.Case(S, "Isolation", "Another tenant and another member cannot list, create, or revoke shares of a briefing, and the pages never show an email address or internal identifiers", IsolationAsync)
            });
        }

        private static Task PagesAsync(CancellationToken token)
        {
            string html = SharePages.Content("# Morning News\n\nHello <script>alert(1)</script> <img src=x onerror=alert(2)>.\n\n[bad](javascript:alert(3)) and [good](https://example.test/a?b=1&c=2).\n\n## Next\n\n- item\n");
            Assert.True(!html.Contains("<script", StringComparison.OrdinalIgnoreCase) && html.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", StringComparison.Ordinal), "script tags are text: " + html);
            Assert.True(!html.Contains("<img", StringComparison.OrdinalIgnoreCase) && html.Contains("&lt;img", StringComparison.Ordinal), "image tags are text");
            Assert.True(!html.Contains("href=\"javascript:", StringComparison.OrdinalIgnoreCase), "javascript links are not links");
            Assert.True(html.Contains("<a rel=\"noopener noreferrer nofollow\" href=\"https://example.test/a?b=1&amp;c=2\">good</a>", StringComparison.Ordinal), "outbound link rel: " + html);
            Assert.True(!html.Contains("Morning News", StringComparison.Ordinal) && html.Contains("<h2>Next</h2>", StringComparison.Ordinal), "leading title line dropped, later headings kept");

            SharedBriefing shared = new SharedBriefing { Briefing = new Briefing { Title = "A <b>bold</b> \"title\"", Markdown = "# x\n\nBody." }, TopicName = "Heat <pumps>" };
            SharePageLinks links = new SharePageLinks { ReadPath = "/v1.0/public/shares/mmns_x", ListenPath = "/v1.0/public/shares/mmns_x/listen", LogoUrl = "https://app.example.test/v1.0/public/assets/logo.png" };
            string page = SharePages.Read(shared, links);
            Assert.True(page.Contains("<h1>A &lt;b&gt;bold&lt;/b&gt; &quot;title&quot;</h1>", StringComparison.Ordinal) && page.Contains("Heat &lt;pumps&gt;", StringComparison.Ordinal) && !page.Contains("<b>bold", StringComparison.Ordinal), "title and topic escaped");
            Assert.True(page.Contains("<html lang=\"en\" data-theme=\"light\">", StringComparison.Ordinal) && page.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", StringComparison.Ordinal)
                && page.Contains("property=\"og:image\" content=\"https://app.example.test/v1.0/public/assets/logo.png\"", StringComparison.Ordinal) && page.Contains("name=\"twitter:card\"", StringComparison.Ordinal), "language, robots, and link preview tags");
            Assert.True(page.Contains("© 2026 Lattice AI LLC", StringComparison.Ordinal) && page.Contains("Learn more at mymorningnews.ai", StringComparison.Ordinal) && page.Contains("href=\"https://mymorningnews.ai\"", StringComparison.Ordinal),
                "copyright and the marketing call to action when sign-up is closed");
            Assert.True(!page.Contains("<script", StringComparison.OrdinalIgnoreCase) && !page.Contains("http://", StringComparison.Ordinal), "no script and no third-party resources");
            links.SignupOpen = true;
            links.SignupUrl = "https://app.example.test/signup";
            Assert.True(SharePages.Read(shared, links).Contains("<a class=\"btn\" href=\"https://app.example.test/signup\" rel=\"noopener noreferrer\">Create your account</a>", StringComparison.Ordinal), "sign-up call to action when sign-up is open");
            string listen = SharePages.Listen(shared, links, null);
            Assert.True(!listen.Contains("<audio", StringComparison.Ordinal) && listen.Contains("class=\"content\"", StringComparison.Ordinal), "without audio the page is the text alone");
            return Task.CompletedTask;
        }

        private static async Task AnonymousAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await MediaSuite.EditionBriefingAsync(f, "Shared Heat", true, token).ConfigureAwait(false);
            MediaAsset m = await MediaSuite.EpisodeForAsync(f, b, token).ConfigureAwait(false);
            Assert.True(m.Status == MediaStatusEnum.Ready, "episode ready: " + m.ErrorMessage);
            string baseUrl = await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false);
            string path = "/v1.0/api/briefings/" + b.Id + "/shares";

            // Briefing text comes from the web: plant markup in it and make sure none of it reaches the page as markup.
            Briefing stored = Assert.NotNull(await f.Server.Context.Database.Briefings.ReadAsync(b.TenantId, b.Id, token).ConfigureAwait(false), "briefing");
            stored.Markdown = (stored.Markdown ?? String.Empty) + "\n\n## Injected\n\nText <script>alert('x')</script> and <iframe src=\"https://evil.test\"></iframe>.\n\n[click](javascript:alert(1)) [source](https://example.test/report)\n";
            await f.Server.Context.Database.Briefings.UpdateAsync(stored, token).ConfigureAwait(false);

            Assert.Equal(0L, (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(200, "empty list").Json<EnumerationResult<BriefingShare>>().TotalRecords, "nothing is shared by default");
            (await Bare(baseUrl + "/v1.0/public/shares/mmns_" + b.Id, token).ConfigureAwait(false)).Expect(404, "a briefing is never public by its identifier");

            BriefingShareCreated created = (await f.User.Client.PostAsync(path, null, token).ConfigureAwait(false)).Expect(201, "create").Json<BriefingShareCreated>();
            Assert.True(created.Secret.StartsWith(BriefingShareService.SecretPrefix, StringComparison.Ordinal) && created.Share.TokenHash == "redacted" && created.Share.Id.StartsWith("shr_", StringComparison.Ordinal), "secret shown once, hash redacted");
            Assert.True(created.Url.EndsWith("/v1.0/public/shares/" + created.Secret, StringComparison.Ordinal) && created.ReadUrl == created.Url && created.ListenUrl == created.Url && created.AudioReady, "one share yields one link");
            Assert.True(!created.Share.ShowOwnerName && created.Share.ViewCount == 0 && created.Share.LastViewedUtc == null, "name hidden by default, not opened yet");

            string pub = baseUrl + "/v1.0/public/shares/" + created.Secret;
            TestResponse read = (await Bare(pub, token).ConfigureAwait(false)).Expect(200, "reading page with no credentials");
            Assert.True(read.Headers["Content-Type"].StartsWith("text/html", StringComparison.Ordinal) && read.Headers["X-Robots-Tag"].Contains("noindex", StringComparison.Ordinal)
                && read.Headers["Referrer-Policy"] == "no-referrer" && read.Headers["Content-Security-Policy"] == SharePages.ContentSecurityPolicy && read.Headers["Cache-Control"].Contains("no-store", StringComparison.Ordinal), "page headers");
            Assert.True(!read.Headers.ContainsKey("Set-Cookie"), "no cookie is set");
            Assert.True(read.Body.Contains("<h1>" + System.Net.WebUtility.HtmlEncode(b.Title) + "</h1>", StringComparison.Ordinal) && read.Body.Contains("Shared Heat", StringComparison.Ordinal)
                && read.Body.Contains("© 2026 Lattice AI LLC", StringComparison.Ordinal) && read.Body.Contains("alt=\"My Morning News\"", StringComparison.Ordinal), "title, edition name, logo, copyright");
            Assert.True(read.Body.Contains("researched and written by My Morning News for one reader's own topic", StringComparison.Ordinal) && read.Body.Contains("Get a briefing like this on your own topics", StringComparison.Ordinal)
                && read.Body.Contains("href=\"https://mymorningnews.ai\"", StringComparison.Ordinal), "both calls to action point at the marketing site while sign-up is closed");
            Assert.True(read.Body.Contains("<audio controls preload=\"metadata\"", StringComparison.Ordinal) && read.Body.Contains("class=\"content\"", StringComparison.Ordinal) && read.Body.IndexOf("<audio", StringComparison.Ordinal) < read.Body.IndexOf("class=\"content\"", StringComparison.Ordinal), "one page carries the player and then the text");
            Assert.True(!read.Body.Contains("<script", StringComparison.OrdinalIgnoreCase) && !read.Body.Contains("<iframe", StringComparison.OrdinalIgnoreCase) && read.Body.Contains("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;", StringComparison.Ordinal)
                && !read.Body.Contains("href=\"javascript:", StringComparison.OrdinalIgnoreCase), "planted markup is shown as text");
            Assert.True(read.Body.Contains("<html lang=\"en\" data-theme=\"light\">", StringComparison.Ordinal) && !read.Body.Contains("prefers-color-scheme", StringComparison.Ordinal)
                && read.Body.Contains("aria-label=\"Appearance\"", StringComparison.Ordinal) && read.Body.Contains("href=\"/v1.0/public/shares/" + created.Secret + "?theme=dark\">Dark</a>", StringComparison.Ordinal), "the page is light unless the visitor chooses dark, and offers both");
            TestResponse dark = (await Bare(pub + "?theme=dark", token).ConfigureAwait(false)).Expect(200, "dark page with no credentials");
            Assert.True(dark.Body.Contains("<html lang=\"en\" data-theme=\"dark\">", StringComparison.Ordinal) && dark.Body.Contains("?theme=dark\" aria-current=\"true\">Dark</a>", StringComparison.Ordinal)
                && dark.Body.Contains("&amp;theme=dark#listen", StringComparison.Ordinal), "the dark choice is drawn, marked, and kept by chapter links");
            Assert.True(read.Body.Contains("href=\"/v1.0/public/shares/" + created.Secret + "/markdown\" download>Download .md</a>", StringComparison.Ordinal), "the page offers the Markdown download");
            TestResponse md = (await Bare(pub + "/markdown", token).ConfigureAwait(false)).Expect(200, "Markdown download with no credentials");
            Assert.True(md.Headers["Content-Type"].StartsWith("text/markdown", StringComparison.Ordinal) && md.Headers["Content-Disposition"].StartsWith("attachment; filename=\"", StringComparison.Ordinal)
                && md.Headers["Content-Disposition"].EndsWith(".md\"", StringComparison.Ordinal) && md.Body.Contains("<script>alert('x')</script>", StringComparison.Ordinal) && md.Headers["X-Content-Type-Options"] == "nosniff" && !md.Headers.ContainsKey("Set-Cookie"), "the briefing Markdown, as written, sent as an attachment that browsers will not render: " + md.Headers["Content-Disposition"]);
            Assert.True(read.Body.Contains("<a rel=\"noopener noreferrer nofollow\" href=\"https://example.test/report\">source</a>", StringComparison.Ordinal), "outbound links carry rel");
            Assert.True(!read.Body.Contains(f.User.User.Email, StringComparison.OrdinalIgnoreCase) && !read.Body.Contains(b.Id, StringComparison.Ordinal) && !read.Body.Contains(b.UserId, StringComparison.Ordinal)
                && !read.Body.Contains(created.Share.Id, StringComparison.Ordinal) && !read.Body.Contains("Shared by", StringComparison.Ordinal), "no email, no identifiers, no name");

            TestResponse listen = (await Bare(pub + "/listen", token).ConfigureAwait(false)).Expect(200, "listening page with no credentials");
            string audioPath = "/v1.0/public/shares/" + created.Secret + "/audio/briefing" + (m.ContentType == "audio/mpeg" ? ".mp3" : ".wav");
            Assert.True(listen.Body.Contains("<audio controls preload=\"metadata\" src=\"" + audioPath + "\"", StringComparison.Ordinal) && listen.Body.Contains("class=\"content\"", StringComparison.Ordinal),
                "the earlier listening address shows the same page, player and text: " + (listen.Body.Contains("<audio", StringComparison.Ordinal) ? listen.Body.Substring(listen.Body.IndexOf("<audio", StringComparison.Ordinal), 160) : "no player"));
            Assert.True(listen.Body.Contains("<nav class=\"chapters\"", StringComparison.Ordinal) && listen.Body.Contains("/v1.0/public/shares/" + created.Secret + "?t=", StringComparison.Ordinal) && !listen.Body.Contains("<script", StringComparison.OrdinalIgnoreCase), "chapter links without script");
            Assert.True(listen.Headers["Content-Security-Policy"].Contains("media-src 'self'", StringComparison.Ordinal) && !listen.Headers.ContainsKey("Set-Cookie"), "listening page headers");
            TestResponse chapter = (await Bare(pub + "/listen?t=1", token).ConfigureAwait(false)).Expect(200, "chapter link");
            Assert.True(chapter.Body.Contains("#t=1\"", StringComparison.Ordinal) == (m.DurationSeconds > 1), "a chapter link starts playback at that time when it is inside the audio (" + m.DurationSeconds + " s)");
            Assert.True(!(await Bare(pub + "/listen?t=999999", token).ConfigureAwait(false)).Expect(200, "start past the end").Body.Contains("#t=", StringComparison.Ordinal), "a start past the end is ignored");

            TestResponse whole = (await Bare(baseUrl + audioPath, token).ConfigureAwait(false)).Expect(200, "audio with no credentials");
            Assert.True(whole.Headers["Content-Type"].StartsWith("audio/", StringComparison.Ordinal) && whole.Headers["Accept-Ranges"] == "bytes" && whole.Headers["Content-Length"] == m.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture), "audio headers");
            TestResponse part = (await Bare(baseUrl + audioPath, token, "bytes=0-9").ConfigureAwait(false)).Expect(206, "audio range");
            Assert.True(part.Headers["Content-Range"] == "bytes 0-9/" + m.Bytes && part.Headers["Content-Length"] == "10", "ranged bytes: " + part.Headers["Content-Range"]);
            (await Bare(baseUrl + audioPath, token, "bytes=" + (m.Bytes + 10) + "-").ConfigureAwait(false)).Expect(416, "range past the end");

            BriefingShare listed = (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<BriefingShare>>().Objects.Single();
            Assert.True(listed.ViewCount == 2 && listed.LastViewedUtc.HasValue && listed.TokenHash == "redacted" && listed.TokenPrefix == created.Secret.Substring(0, 10), "two openings counted (chapter reloads and audio are not): " + listed.ViewCount);

            // The owner's name appears only when asked for, and the email address never does.
            User owner = Assert.NotNull(await f.Server.Context.Database.Users.ReadAsync(b.TenantId, b.UserId, token).ConfigureAwait(false), "owner");
            owner.FirstName = "Avery";
            owner.LastName = "Quill";
            await f.Server.Context.Database.Users.UpdateAsync(owner, token).ConfigureAwait(false);
            Assert.True(!(await Bare(pub, token).ConfigureAwait(false)).Body.Contains("Avery", StringComparison.Ordinal), "the name stays hidden on a share that did not ask for it");
            BriefingShareCreated named = (await f.User.Client.PostAsync(path, new { showOwnerName = true, expiresInDays = 1 }, token).ConfigureAwait(false)).Expect(201, "named share").Json<BriefingShareCreated>();
            TestResponse namedPage = (await Bare(named.ReadUrl.Replace(f.Server.Context.Settings.Rest.PublicBaseUrl, baseUrl, StringComparison.Ordinal), token).ConfigureAwait(false)).Expect(200, "named page");
            Assert.True(namedPage.Body.Contains("Shared by Avery Quill", StringComparison.Ordinal) && !namedPage.Body.Contains(f.User.User.Email, StringComparison.OrdinalIgnoreCase), "name shown on request, never the email");

            // With sign-up open the call to action is the sign-up form.
            HostingSettings h = f.Server.Context.Settings.Hosting;
            bool signup = h.PublicSignupEnabled;
            try
            {
                h.PublicSignupEnabled = true;
                Assert.True((await Bare(pub, token).ConfigureAwait(false)).Body.Contains("href=\"" + f.Server.Context.Settings.Rest.AppBaseUrl + "/signup\" rel=\"noopener noreferrer\">Create your account</a>", StringComparison.Ordinal), "sign-up link when sign-up is open");
            }
            finally
            {
                h.PublicSignupEnabled = signup;
            }

            // A briefing with no audio: the reading page works and the listening page says so plainly.
            Briefing quiet = await MediaSuite.EditionBriefingAsync(f, "Quiet Heat", false, token).ConfigureAwait(false);
            BriefingShareCreated q = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + quiet.Id + "/shares", new { expiresInDays = 3 }, token).ConfigureAwait(false)).Expect(201, "share without audio").Json<BriefingShareCreated>();
            Assert.True(!q.AudioReady, "no audio yet");
            string qpub = baseUrl + "/v1.0/public/shares/" + q.Secret;
            (await Bare(qpub, token).ConfigureAwait(false)).Expect(200, "reading page without audio");
            TestResponse qListen = (await Bare(qpub + "/listen", token).ConfigureAwait(false)).Expect(200, "listening page without audio");
            Assert.True(!qListen.Body.Contains("<audio", StringComparison.Ordinal) && qListen.Body.Contains("class=\"content\"", StringComparison.Ordinal), "without audio the page is the text alone");
            (await Bare(qpub + "/audio/briefing.wav", token).ConfigureAwait(false)).Expect(404, "no audio to stream");

            ClientConfigResponse cfg = (await Bare(baseUrl + "/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config").Json<ClientConfigResponse>();
            Assert.True(cfg.SharingEnabled && cfg.SharingMaxExpiryDays == 14 && cfg.SharingDefaultExpiryDays == 7, "client config reports sharing and its limits");
            await DrainEmailAsync(f, token).ConfigureAwait(false);
        }

        private static async Task ExpiryAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await MediaSuite.EditionBriefingAsync(f, "Expiring Heat", false, token).ConfigureAwait(false);
            string baseUrl = await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false);
            string path = "/v1.0/api/briefings/" + b.Id + "/shares";

            DateTime before = DateTime.UtcNow;
            BriefingShareCreated byDefault = (await f.User.Client.PostAsync(path, new { }, token).ConfigureAwait(false)).Expect(201, "default expiry").Json<BriefingShareCreated>();
            Assert.True(Math.Abs((byDefault.Share.ExpiresUtc - before.AddDays(7)).TotalMinutes) < 5, "7 days when the request does not say: " + byDefault.Share.ExpiresUtc.ToString("o"));

            TestResponse tooLong = (await f.User.Client.PostAsync(path, new { expiresInDays = 15 }, token).ConfigureAwait(false)).Expect(400, "15 days refused");
            ErrorResponse err = tooLong.Json<ErrorResponse>();
            Assert.True(err.Error == "ValidationFailed" && err.Message == "expiresInDays must be between 1 and 14." && err.Details != null && err.Details["maximum"] == "14" && err.Details["minimum"] == "1", "a clear 400: " + tooLong.Body);
            (await f.User.Client.PostAsync(path, new { expiresInDays = 0 }, token).ConfigureAwait(false)).Expect(400, "0 days refused");
            (await f.User.Client.PostAsync(path, new { expiresInDays = -3 }, token).ConfigureAwait(false)).Expect(400, "negative days refused");
            (await f.User.Client.PostAsync(path, "{\"expiresInDays\":\"never\"}", token).ConfigureAwait(false)).Expect(400, "a non-number is refused");
            Assert.Equal(1L, (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Json<EnumerationResult<BriefingShare>>().TotalRecords, "refused requests create nothing");

            BriefingShareCreated longest = (await f.User.Client.PostAsync(path, new { expiresInDays = 14 }, token).ConfigureAwait(false)).Expect(201, "14 days accepted").Json<BriefingShareCreated>();
            Assert.True(Math.Abs((longest.Share.ExpiresUtc - DateTime.UtcNow.AddDays(14)).TotalMinutes) < 5, "14 days stored");
            BriefingShareCreated shortest = (await f.User.Client.PostAsync(path, new { expiresInDays = 1 }, token).ConfigureAwait(false)).Expect(201, "1 day accepted").Json<BriefingShareCreated>();
            Assert.True(Math.Abs((shortest.Share.ExpiresUtc - DateTime.UtcNow.AddDays(1)).TotalMinutes) < 5, "1 day stored");

            // The maximum is a setting; the server follows it live.
            SharingSettings sharing = f.Server.Context.Settings.Sharing;
            int max = sharing.MaxExpiryDays;
            try
            {
                sharing.MaxExpiryDays = 3;
                (await f.User.Client.PostAsync(path, new { expiresInDays = 4 }, token).ConfigureAwait(false)).Expect(400, "above a lowered maximum");
                BriefingShareCreated capped = (await f.User.Client.PostAsync(path, null, token).ConfigureAwait(false)).Expect(201, "default under a lowered maximum").Json<BriefingShareCreated>();
                Assert.True(Math.Abs((capped.Share.ExpiresUtc - DateTime.UtcNow.AddDays(3)).TotalMinutes) < 5, "the default never exceeds the maximum");
            }
            finally
            {
                sharing.MaxExpiryDays = max;
            }

            string pub = baseUrl + "/v1.0/public/shares/" + shortest.Secret;
            (await Bare(pub, token).ConfigureAwait(false)).Expect(200, "live before expiry");
            BriefingShare row = Assert.NotNull(await f.Server.Context.Database.BriefingShares.ReadAsync(b.TenantId, shortest.Share.Id, token).ConfigureAwait(false), "share row");
            row.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
            await f.Server.Context.Database.BriefingShares.UpdateAsync(row, token).ConfigureAwait(false);
            TestResponse expired = (await Bare(pub, token).ConfigureAwait(false)).Expect(404, "expired reading page");
            (await Bare(pub + "/listen", token).ConfigureAwait(false)).Expect(404, "expired listening page");
            (await Bare(pub + "/markdown", token).ConfigureAwait(false)).Expect(404, "expired Markdown download");
            (await Bare(pub + "/audio/briefing.wav", token).ConfigureAwait(false)).Expect(404, "expired audio");
            TestResponse unknown = (await Bare(baseUrl + "/v1.0/public/shares/mmns_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", token).ConfigureAwait(false)).Expect(404, "unknown link");
            Assert.True(expired.Body == unknown.Body && expired.Headers["Content-Type"].StartsWith("text/html", StringComparison.Ordinal), "expired and unknown links get the identical page");
            Assert.True(expired.Body.Contains("This link is not available", StringComparison.Ordinal) && expired.Body.Contains("alt=\"My Morning News\"", StringComparison.Ordinal) && expired.Body.Contains("© 2026 Lattice AI LLC", StringComparison.Ordinal)
                && !expired.Body.Contains(b.Title, StringComparison.Ordinal), "branded 404 that reveals nothing");
            Assert.True(expired.Headers["X-Robots-Tag"].Contains("noindex", StringComparison.Ordinal) && expired.Headers["Referrer-Policy"] == "no-referrer", "404 headers");
            BriefingShare listed = (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Json<EnumerationResult<BriefingShare>>().Objects.Single(x => x.Id == shortest.Share.Id);
            Assert.True(listed.ExpiresUtc < DateTime.UtcNow && !listed.RevokedUtc.HasValue, "an expired share stays listed with its expiry");
            (await f.User.Client.DeleteAsync(path + "/" + shortest.Share.Id + "?purge=true", token).ConfigureAwait(false)).Expect(204, "purge an expired share");
            Assert.True((await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Json<EnumerationResult<BriefingShare>>().Objects.All(x => x.Id != shortest.Share.Id), "purged");
            await DrainEmailAsync(f, token).ConfigureAwait(false);
        }

        private static async Task RevokeAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await MediaSuite.EditionBriefingAsync(f, "Revoked Heat", true, token).ConfigureAwait(false);
            await MediaSuite.EpisodeForAsync(f, b, token).ConfigureAwait(false);
            string baseUrl = await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false);
            string path = "/v1.0/api/briefings/" + b.Id + "/shares";
            BriefingShareCreated one = (await f.User.Client.PostAsync(path, new { expiresInDays = 7 }, token).ConfigureAwait(false)).Expect(201, "first share").Json<BriefingShareCreated>();
            BriefingShareCreated two = (await f.User.Client.PostAsync(path, new { expiresInDays = 7 }, token).ConfigureAwait(false)).Expect(201, "second share").Json<BriefingShareCreated>();
            string pubOne = baseUrl + "/v1.0/public/shares/" + one.Secret;
            string pubTwo = baseUrl + "/v1.0/public/shares/" + two.Secret;
            TestResponse firstRead = (await Bare(pubOne, token).ConfigureAwait(false)).Expect(200, "first live");
            SaveSamples(firstRead.Body, (await Bare(pubOne + "/listen", token).ConfigureAwait(false)).Expect(200, "first listening page").Body,
                (await Bare(baseUrl + "/v1.0/public/shares/mmns_sample", token).ConfigureAwait(false)).Expect(404, "sample of the 404 page").Body);
            (await Bare(pubOne + "/audio/briefing.wav", token, "bytes=0-3").ConfigureAwait(false)).Expect(206, "first audio live");

            // The server-wide switch.
            SharingSettings sharing = f.Server.Context.Settings.Sharing;
            try
            {
                sharing.Enabled = false;
                TestResponse off = (await Bare(pubOne, token).ConfigureAwait(false)).Expect(404, "reading page while sharing is off");
                (await Bare(pubOne + "/listen", token).ConfigureAwait(false)).Expect(404, "listening page while sharing is off");
                (await Bare(pubOne + "/audio/briefing.wav", token).ConfigureAwait(false)).Expect(404, "audio while sharing is off");
                Assert.True(off.Body.Contains("This link is not available", StringComparison.Ordinal), "the same 404 page");
                TestResponse refused = (await f.User.Client.PostAsync(path, new { expiresInDays = 7 }, token).ConfigureAwait(false)).Expect(409, "create while sharing is off");
                Assert.Equal("SharingDisabled", refused.Json<ErrorResponse>().Error, "error code");
                Assert.True(!(await Bare(baseUrl + "/v1.0/api/client-config", token).ConfigureAwait(false)).Json<ClientConfigResponse>().SharingEnabled, "client config reports sharing off");
                Assert.Equal(2L, (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(200, "list while off").Json<EnumerationResult<BriefingShare>>().TotalRecords, "shares can still be listed");
            }
            finally
            {
                sharing.Enabled = true;
            }

            (await Bare(pubOne, token).ConfigureAwait(false)).Expect(200, "live again when sharing is turned back on");

            // Revoking one share leaves the other alone.
            (await f.User.Client.DeleteAsync(path + "/" + one.Share.Id + "?purge=true", token).ConfigureAwait(false)).Expect(204, "revoke (purge ignored on a live share)");
            TestResponse revoked = (await Bare(pubOne, token).ConfigureAwait(false)).Expect(404, "revoked reading page");
            (await Bare(pubOne + "/listen", token).ConfigureAwait(false)).Expect(404, "revoked listening page");
            (await Bare(pubOne + "/audio/briefing.wav", token).ConfigureAwait(false)).Expect(404, "revoked audio");
            TestResponse unknown = (await Bare(baseUrl + "/v1.0/public/shares/mmns_nope", token).ConfigureAwait(false)).Expect(404, "unknown");
            Assert.True(revoked.Body == unknown.Body, "revoked and unknown links get the identical page");
            (await Bare(pubTwo, token).ConfigureAwait(false)).Expect(200, "the other share still works");
            List<BriefingShare> after = (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Json<EnumerationResult<BriefingShare>>().Objects;
            Assert.True(after.Single(x => x.Id == one.Share.Id).RevokedUtc.HasValue && !after.Single(x => x.Id == two.Share.Id).RevokedUtc.HasValue, "revocation recorded on one share only");
            (await f.User.Client.DeleteAsync(path + "/" + one.Share.Id, token).ConfigureAwait(false)).Expect(204, "revoking twice is harmless");
            (await f.User.Client.DeleteAsync(path + "/" + one.Share.Id + "?purge=true", token).ConfigureAwait(false)).Expect(204, "purge the revoked share");
            (await f.User.Client.DeleteAsync(path + "/" + one.Share.Id, token).ConfigureAwait(false)).Expect(404, "gone");

            long created = await f.Server.Context.Database.AuditRecords.CountAsync(new AuditRecordFilter { TenantId = b.TenantId, EventType = AuditEventTypeEnum.ShareCreated }, token).ConfigureAwait(false);
            long revokedCount = await f.Server.Context.Database.AuditRecords.CountAsync(new AuditRecordFilter { TenantId = b.TenantId, EventType = AuditEventTypeEnum.ShareRevoked }, token).ConfigureAwait(false);
            Assert.True(created == 2 && revokedCount == 1, "audit records: " + created + " created, " + revokedCount + " revoked");

            // Deleting the briefing makes the remaining share dead and removes its row.
            (await f.User.Client.DeleteAsync("/v1.0/api/briefings/" + b.Id, token).ConfigureAwait(false)).Expect(204, "delete briefing");
            (await Bare(pubTwo, token).ConfigureAwait(false)).Expect(404, "share of a deleted briefing");
            (await Bare(pubTwo + "/audio/briefing.wav", token).ConfigureAwait(false)).Expect(404, "audio of a deleted briefing");
            Assert.True(await f.Server.Context.Database.BriefingShares.ReadByIdAsync(two.Share.Id, token).ConfigureAwait(false) == null, "the share row went with the briefing");
            await DrainEmailAsync(f, token).ConfigureAwait(false);
        }

        private static async Task IsolationAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await MediaSuite.EditionBriefingAsync(f, "Private Heat", false, token).ConfigureAwait(false);
            string baseUrl = await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false);
            string path = "/v1.0/api/briefings/" + b.Id + "/shares";
            BriefingShareCreated mine = (await f.User.Client.PostAsync(path, new { expiresInDays = 2 }, token).ConfigureAwait(false)).Expect(201, "my share").Json<BriefingShareCreated>();

            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant otherTenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser outsider = await TestFixtures.CreateUserAsync(admin, otherTenant.Id, true, token).ConfigureAwait(false);
            (await outsider.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(404, "another tenant cannot list");
            (await outsider.Client.PostAsync(path, new { expiresInDays = 2 }, token).ConfigureAwait(false)).Expect(404, "another tenant cannot create");
            (await outsider.Client.DeleteAsync(path + "/" + mine.Share.Id, token).ConfigureAwait(false)).Expect(404, "another tenant cannot revoke");

            TestUser colleague = await TestFixtures.CreateUserAsync(admin, b.TenantId, false, token).ConfigureAwait(false);
            (await colleague.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(404, "another member cannot list");
            (await colleague.Client.PostAsync(path, null, token).ConfigureAwait(false)).Expect(404, "another member cannot create");
            (await colleague.Client.DeleteAsync(path + "/" + mine.Share.Id, token).ConfigureAwait(false)).Expect(404, "another member cannot revoke");

            TestApiClient anon = new TestApiClient(baseUrl);
            (await anon.GetAsync(path, token).ConfigureAwait(false)).Expect(401, "listing needs a sign-in");
            (await anon.PostAsync(path, null, token).ConfigureAwait(false)).Expect(401, "creating needs a sign-in");

            // A share of one briefing cannot be revoked through another briefing's path.
            Briefing second = await MediaSuite.EditionBriefingAsync(f, "Second Heat", false, token).ConfigureAwait(false);
            (await f.User.Client.DeleteAsync("/v1.0/api/briefings/" + second.Id + "/shares/" + mine.Share.Id, token).ConfigureAwait(false)).Expect(404, "wrong briefing");

            (await Bare(baseUrl + "/v1.0/public/shares/" + mine.Secret, token).ConfigureAwait(false)).Expect(200, "still live after every refused attempt");
            Assert.Equal(1L, (await f.User.Client.GetAsync(path, token).ConfigureAwait(false)).Json<EnumerationResult<BriefingShare>>().TotalRecords, "nothing was added or removed");

            // A tenant administrator of the same tenant can see and revoke it.
            TestUser tenantAdmin = await TestFixtures.CreateUserAsync(admin, b.TenantId, true, token).ConfigureAwait(false);
            Assert.Equal(1L, (await tenantAdmin.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(200, "tenant administrator lists").Json<EnumerationResult<BriefingShare>>().TotalRecords, "tenant administrator sees the share");
            (await tenantAdmin.Client.DeleteAsync(path + "/" + mine.Share.Id, token).ConfigureAwait(false)).Expect(204, "tenant administrator revokes");
            (await Bare(baseUrl + "/v1.0/public/shares/" + mine.Secret, token).ConfigureAwait(false)).Expect(404, "revoked by the administrator");

            // The link secret never reaches request history.
            await Task.Delay(300, token).ConfigureAwait(false);
            List<RequestHistoryEntry> history = (await f.Server.Context.Database.RequestHistory.EnumerateAsync(new RequestHistoryFilter { PageSize = 1000 }, token).ConfigureAwait(false)).Objects;
            Assert.True(history.All(e => !(e.Path ?? String.Empty).Contains(mine.Secret, StringComparison.Ordinal) && !(e.Url ?? String.Empty).Contains(mine.Secret, StringComparison.Ordinal)
                && !(e.ResponseBody ?? String.Empty).Contains(mine.Secret, StringComparison.Ordinal)), "request history holds no link secret");
            await DrainEmailAsync(f, token).ConfigureAwait(false);
        }

        // The runs behind these briefings queue digest emails on the shared server. Deliver them here so the outbox other
        // suites work through (50 messages a pass) is no longer than it was before this suite ran.
        private static async Task DrainEmailAsync(PipelineFixture f, CancellationToken token)
        {
            for (int pass = 0; pass < 20; pass++)
            {
                if (await f.Server.Context.Email.DeliverDueAsync(DateTime.UtcNow, token).ConfigureAwait(false) == 0) break;
            }
        }

        // Set MYMORNINGNEWS_SHARE_SAMPLE_DIR to keep the pages this suite fetched, for a look at the real HTML.
        private static void SaveSamples(string read, string listen, string notFound)
        {
            string? dir = Environment.GetEnvironmentVariable("MYMORNINGNEWS_SHARE_SAMPLE_DIR");
            if (String.IsNullOrWhiteSpace(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "share-read.html"), read);
            File.WriteAllText(Path.Combine(dir, "share-listen.html"), listen);
            File.WriteAllText(Path.Combine(dir, "share-not-found.html"), notFound);
        }

        private static async Task<TestResponse> Bare(string url, CancellationToken token, string? range = null)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (range != null) req.Headers.TryAddWithoutValidation("Range", range);
                Assert.True(req.Headers.Authorization == null && !req.Headers.Contains("Cookie") && !req.Headers.Contains("x-token") && !req.Headers.Contains("x-api-key"), "the request carries no credentials");
                using (HttpResponseMessage resp = await _Bare.SendAsync(req, token).ConfigureAwait(false))
                {
                    TestResponse result = new TestResponse { StatusCode = (int)resp.StatusCode, Body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false) };
                    foreach (KeyValuePair<string, IEnumerable<string>> h in resp.Headers) result.Headers[h.Key] = String.Join(",", h.Value);
                    foreach (KeyValuePair<string, IEnumerable<string>> h in resp.Content.Headers) result.Headers[h.Key] = String.Join(",", h.Value);
                    return result;
                }
            }
        }
    }
}
