namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using MyMorningNews.Core.Audio;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Audio and podcasts (plan section 22): scripts, segments, WAV joining, episodes with chapters, retries and failure,
    /// voices, private feeds, and public artwork.
    /// </summary>
    public static class MediaSuite
    {
        private const string S = "Media";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Audio and podcasts", new[]
            {
                Cases.Case(S, "Script", "Scripts parse into chapters and speakers, are cleaned for speech, and split under the limit", ScriptAsync, "fast"),
                Cases.Case(S, "Wav", "WAV segments join with exact durations", WavAsync, "fast"),
                Cases.Case(S, "EditionAudio", "An edition with audio produces an episode with chapters aligned to its sections", EditionAudioAsync),
                Cases.Case(S, "ScriptFallback", "Without a script model the episode is spoken from the briefing text", ScriptFallbackAsync),
                Cases.Case(S, "Failure", "Speech failures are retried and then marked failed", FailureAsync),
                Cases.Case(S, "Voices", "Voices are listed, managed by administrators, tested, and defaulted", VoicesAsync),
                Cases.Case(S, "Feed", "A private feed lists episodes with enclosures that stream, and stops when revoked", FeedAsync)
            });
        }

        private static Task ScriptAsync(CancellationToken token)
        {
            List<ScriptPart> parts = SpeechScript.Parse("Hello there.\n## By topic\nHOST_A: Rebates expanded [1] at https://x.test/a.\nmore from A.\nHOST_B: **Really?**\n## Next\n- One item\n");
            Assert.True(parts.Count == 3 && parts[0].Heading == "Introduction" && parts[1].Heading == "By topic" && parts[2].Heading == "Next", "chapters: " + String.Join(", ", parts.Select(p => p.Heading)));
            Assert.True(parts[1].Lines.Count == 2 && parts[1].Lines[0].Speaker == "HOST_A" && parts[1].Lines[0].Text == "Rebates expanded at more from A." && parts[1].Lines[1].Text == "Really?", "speakers merged and cleaned: " + parts[1].Lines[0].Text);
            Assert.Equal("One item", parts[2].Lines[0].Text, "list marker removed");
            Assert.Equal("See the report on the site.", SpeechScript.Clean("See the [report](https://a.test/r) on the site [2, 3]."), "links and citations cleaned");

            string md = "# Morning News for Thursday\n\nOpening line.\n\n## Top of the news\n\n- **Rebates expand** [1]: The state announced rebates.\n\n## Sources\n\n1. [A](https://a.test)\n";
            string narr = SpeechScript.FromBriefing(md, "narrator", "Good morning.");
            Assert.True(narr.Contains("## Top of the news", StringComparison.Ordinal) && !narr.Contains("Sources", StringComparison.Ordinal) && !narr.Contains("https://", StringComparison.Ordinal) && !narr.Contains("[1]", StringComparison.Ordinal), "narrator fallback: " + narr);
            string two = SpeechScript.FromBriefing(md, "two-host", "Good morning.");
            Assert.True(two.Contains("HOST_A: Good morning.", StringComparison.Ordinal) && two.Contains("HOST_B:", StringComparison.Ordinal), "two-host fallback alternates");

            string longText = String.Join(" ", Enumerable.Range(1, 40).Select(i => "Sentence number " + i + " is here."));
            List<SpeechSegment> segs = SpeechScript.Segment(new List<ScriptPart> { new ScriptPart { Heading = "A", Lines = new List<ScriptLine> { new ScriptLine { Speaker = "HOST_A", Text = longText }, new ScriptLine { Speaker = "HOST_B", Text = "Short." } } } }, 200);
            Assert.True(segs.All(x => x.Text.Length <= 200) && segs.Count >= 4 && segs.Last().Speaker == "HOST_B" && segs.Take(segs.Count - 1).All(x => x.Speaker == "HOST_A"), "segments split under the limit by speaker");
            Assert.True(segs.Take(segs.Count - 1).All(x => x.Text.EndsWith(".", StringComparison.Ordinal)), "split at sentence boundaries");
            return Task.CompletedTask;
        }

        private static Task WavAsync(CancellationToken token)
        {
            byte[] a = WavAudio.Tone(0.5, 440, 8000);
            byte[] b = WavAudio.Tone(0.25, 330, 8000);
            WavInfo? ia = WavAudio.Parse(a);
            Assert.True(ia != null && ia.SampleRate == 8000 && ia.Channels == 1 && Math.Abs(ia.DurationSeconds - 0.5) < 0.001, "parse");
            WavInfo? joined = WavAudio.Parse(WavAudio.Concatenate(new List<byte[]> { a, b }));
            Assert.True(joined != null && Math.Abs(joined.DurationSeconds - 0.75) < 0.001, "joined duration");
            bool mismatch = false;
            try
            {
                WavAudio.Concatenate(new List<byte[]> { a, WavAudio.Tone(0.1, 440, 16000) });
            }
            catch (ArgumentException)
            {
                mismatch = true;
            }

            Assert.True(mismatch && WavAudio.Parse(Encoding.UTF8.GetBytes("not audio at all, definitely not")) == null, "format mismatch and non-WAV rejected");
            return Task.CompletedTask;
        }

        private static async Task<Briefing> EditionBriefingAsync(PipelineFixture f, string name, bool audio, CancellationToken token)
        {
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = name, freshResearchBeforeCompose = false, audioEnabled = audio, lookbackHours = 48 }, token).ConfigureAwait(false))
                .Expect(201, "edition").Json<EditionResponse>();
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topics");
            Briefing queued = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose").Json<Briefing>();
            await f.Server.Context.Briefings.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            Briefing b = (await f.Server.Context.Database.Briefings.ReadAsync(queued.TenantId, queued.Id, token).ConfigureAwait(false))!;
            Assert.True(b.Status == BriefingStatusEnum.Ready, "briefing ready: " + b.ErrorMessage);
            return b;
        }

        private static async Task<MediaAsset> EpisodeForAsync(PipelineFixture f, Briefing b, CancellationToken token)
        {
            for (int i = 0; i < 5; i++)
            {
                await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                EnumerationResult<MediaAsset> list = (await f.User.Client.GetAsync("/v1.0/api/media?briefingId=" + b.Id, token).ConfigureAwait(false)).Json<EnumerationResult<MediaAsset>>();
                MediaAsset? m = list.Items.OrderByDescending(x => x.CreatedUtc).FirstOrDefault();
                if (m != null && (m.Status == MediaStatusEnum.Ready || m.Status == MediaStatusEnum.Failed)) return m;
            }

            throw new InvalidOperationException("No finished episode for briefing " + b.Id);
        }

        private static async Task EditionAudioAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await EditionBriefingAsync(f, "Audio Heat", true, token).ConfigureAwait(false);
            MediaAsset m = await EpisodeForAsync(f, b, token).ConfigureAwait(false);
            bool mp3 = await f.Server.Context.Audio.Mixer.FfmpegAvailableAsync(token).ConfigureAwait(false);
            Assert.True(m.Status == MediaStatusEnum.Ready && m.ScriptFromModel && m.Format == "two-host" && m.DurationSeconds > 0 && m.SegmentCount >= 5 && m.ArtifactId != null, "episode ready: " + m.Status + " " + m.ErrorMessage);
            Assert.Equal(mp3 ? "audio/mpeg" : "audio/wav", m.ContentType, "encoded as " + (mp3 ? "MP3 with ffmpeg" : "WAV without ffmpeg"));

            MediaDetailResponse d = (await f.User.Client.GetAsync("/v1.0/api/media/" + m.Id, token).ConfigureAwait(false)).Expect(200, "detail").Json<MediaDetailResponse>();
            Assert.True(d.Chapters.Count == 3 && d.Chapters[0].StartSeconds == 0 && d.Chapters.Zip(d.Chapters.Skip(1), (x, y) => Math.Abs(x.EndSeconds - y.StartSeconds) < 0.01).All(ok => ok)
                && Math.Abs(d.Chapters.Last().EndSeconds - m.DurationSeconds) < 0.05, "contiguous chapters: " + String.Join(", ", d.Chapters.Select(c => c.Title + " " + c.StartSeconds + "-" + c.EndSeconds)));
            Assert.True(d.Chapters[1].Title == "By topic" && d.Chapters[1].BriefingSectionId != null, "chapter aligned to the briefing section");
            string speech = String.Join("\n", StubModelServer.Shared.SpeechRequests);
            Assert.True(speech.Contains("am_michael", StringComparison.Ordinal) && speech.Contains("af_heart", StringComparison.Ordinal) && !speech.Contains("https://example.test", StringComparison.Ordinal) && !speech.Contains("[1]", StringComparison.Ordinal),
                "two voices; no URLs or citation markers spoken");

            TestResponse range = (await f.User.Client.SendAsync(HttpMethod.Get, d.AudioPath!, null, new Dictionary<string, string> { { "Range", "bytes=0-99" } }, token).ConfigureAwait(false)).Expect(206, "seek");
            Assert.True(range.Headers["Content-Range"].StartsWith("bytes 0-99/", StringComparison.Ordinal), "ranged audio");
            TestResponse chapters = (await f.User.Client.GetAsync(d.ChaptersPath!, token).ConfigureAwait(false)).Expect(200, "chapters json");
            Assert.True(chapters.Body.Contains("\"version\":\"1.2.0\"", StringComparison.Ordinal) && chapters.Body.Contains("\"title\":\"By topic\"", StringComparison.Ordinal), "Podcasting 2.0 chapters");
            Briefing linked = (await f.Server.Context.Database.Briefings.ReadAsync(b.TenantId, b.Id, token).ConfigureAwait(false))!;
            Assert.Equal(m.Id, linked.MediaAssetId, "briefing links its audio");

            MediaAsset again = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + b.Id + "/audio", null, token).ConfigureAwait(false)).Expect(202, "remake").Json<MediaAsset>();
            Assert.True(again.Id != m.Id && again.Status == MediaStatusEnum.Queued, "new episode queued");
            (await f.User.Client.PostAsync("/v1.0/api/briefings/" + b.Id + "/audio", null, token).ConfigureAwait(false)).Expect(409, "one at a time");
            await EpisodeForAsync(f, b, token).ConfigureAwait(false);
            (await f.User.Client.DeleteAsync("/v1.0/api/media/" + m.Id, token).ConfigureAwait(false)).Expect(204, "delete episode");
            (await f.User.Client.GetAsync(d.AudioPath!, token).ConfigureAwait(false)).Expect(404, "its audio file is gone");

            Run run = (await f.User.Client.GetAsync("/v1.0/api/runs?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Run>>().Items.First();
            Briefing runBriefing = (await f.User.Client.GetAsync("/v1.0/api/briefings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().Items.Single();
            Assert.Equal(0, (await f.User.Client.GetAsync("/v1.0/api/media?briefingId=" + runBriefing.Id, token).ConfigureAwait(false)).Json<EnumerationResult<MediaAsset>>().Items.Count, "run briefings have no audio unless the template turns it on");
        }

        private static async Task ScriptFallbackAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await EditionBriefingAsync(f, "FAILSCRIPT Heat", true, token).ConfigureAwait(false);
            MediaAsset m = await EpisodeForAsync(f, b, token).ConfigureAwait(false);
            MediaAsset full = (await f.Server.Context.Database.MediaAssets.ReadAsync(m.TenantId, m.Id, token).ConfigureAwait(false))!;
            Assert.True(full.Status == MediaStatusEnum.Ready && !full.ScriptFromModel && full.ScriptMarkdown!.Contains("## By topic", StringComparison.Ordinal) && !full.ScriptMarkdown.Contains("Sources", StringComparison.Ordinal),
                "spoken from the briefing: " + full.Status + " " + full.ErrorMessage);
        }

        private static async Task FailureAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            VoiceProfile bad = (await f.User.Client.PostAsync("/v1.0/api/voices", new { name = "Broken " + Guid.NewGuid().ToString("N").Substring(0, 6), voice = "nobody", endpoint = StubModelServer.Shared.BaseUrl + "/ttsfail" }, token).ConfigureAwait(false))
                .Expect(201, "broken voice").Json<VoiceProfile>();
            Briefing b = await EditionBriefingAsync(f, "Broken Voice", false, token).ConfigureAwait(false);
            MediaAsset queued = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + b.Id + "/audio", new { voiceProfileId = bad.Id }, token).ConfigureAwait(false)).Expect(202, "queue").Json<MediaAsset>();
            MediaAsset? m = null;
            for (int i = 0; i < 5; i++)
            {
                await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                m = await f.Server.Context.Database.MediaAssets.ReadAsync(queued.TenantId, queued.Id, token).ConfigureAwait(false);
                if (m!.Status == MediaStatusEnum.Failed) break;
            }

            Assert.True(m!.Status == MediaStatusEnum.Failed && m.Attempts == f.Server.Context.Settings.Tts.MaxAttempts && m.ErrorMessage!.Contains("400", StringComparison.Ordinal), "failed after retries: " + m.Status + " " + m.Attempts + " " + m.ErrorMessage);
            (await f.User.Client.PostAsync("/v1.0/api/briefings/" + b.Id + "/audio", null, token).ConfigureAwait(false)).Expect(202, "a failed episode can be remade");
            (await f.User.Client.DeleteAsync("/v1.0/api/voices/" + bad.Id, token).ConfigureAwait(false)).Expect(204, "delete voice");
        }

        private static async Task VoicesAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            EnumerationResult<VoiceProfile> list = (await member.Client.GetAsync("/v1.0/api/voices", token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<VoiceProfile>>();
            Assert.True(list.Items.Count == 1 && list.Items[0].Name == AudioService.DefaultVoiceName && list.Items[0].IsDefault && list.Items[0].SecondaryVoice == "am_michael", "default voice seeded");
            (await member.Client.PostAsync("/v1.0/api/voices", new { name = "Mine", voice = "af_sky" }, token).ConfigureAwait(false)).Expect(403, "members cannot add voices");
            TestResponse sample = (await member.Client.PostAsync("/v1.0/api/voices/" + list.Items[0].Id + "/test", new { text = "Testing one two." }, token).ConfigureAwait(false)).Expect(200, "test voice");
            Assert.True(sample.Headers["Content-Type"].StartsWith("audio/wav", StringComparison.Ordinal) && sample.Body.StartsWith("RIFF", StringComparison.Ordinal), "sample audio");
            VoiceProfile v = (await ta.Client.PostAsync("/v1.0/api/voices", new { name = "Bright", voice = "af_bella", speed = 3.0, isDefault = true }, token).ConfigureAwait(false)).Expect(201, "create").Json<VoiceProfile>();
            Assert.True(v.Speed == 2.0 && v.IsDefault && v.Model == "kokoro", "speed clamped, default model");
            EnumerationResult<VoiceProfile> after = (await member.Client.GetAsync("/v1.0/api/voices", token).ConfigureAwait(false)).Json<EnumerationResult<VoiceProfile>>();
            Assert.True(after.Items.Count(x => x.IsDefault) == 1 && after.Items.Single(x => x.IsDefault).Id == v.Id, "one default");
            (await ta.Client.PutAsync("/v1.0/api/voices/" + v.Id, new { endpoint = "ftp://bad" }, token).ConfigureAwait(false)).Expect(400, "endpoint validated");
            (await ta.Client.PutAsync("/v1.0/api/voices/" + v.Id, new { credentialId = "crd_missing" }, token).ConfigureAwait(false)).Expect(400, "credential validated");
            (await ta.Client.DeleteAsync("/v1.0/api/voices/" + v.Id, token).ConfigureAwait(false)).Expect(204, "delete");
        }

        private static async Task FeedAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Briefing b = await EditionBriefingAsync(f, "Feed Heat", true, token).ConfigureAwait(false);
            MediaAsset m = await EpisodeForAsync(f, b, token).ConfigureAwait(false);
            FeedTokenCreated created = (await f.User.Client.PostAsync("/v1.0/api/feed-tokens", new { name = "Overcast" }, token).ConfigureAwait(false)).Expect(201, "token").Json<FeedTokenCreated>();
            Assert.True(created.Secret.StartsWith(AudioService.FeedTokenPrefix, StringComparison.Ordinal) && created.FeedUrl.EndsWith("/podcast.xml", StringComparison.Ordinal) && created.Token.TokenHash == "redacted", "secret shown once, hash redacted");
            List<FeedToken> tokens = (await f.User.Client.GetAsync("/v1.0/api/feed-tokens", token).ConfigureAwait(false)).Json<List<FeedToken>>();
            Assert.True(tokens.Count == 1 && tokens[0].TokenPrefix == created.Secret.Substring(0, 10) && tokens[0].TokenHash == "redacted", "listed without the hash");

            TestApiClient anon = new TestApiClient(await SharedTestServer.GetBaseUrlAsync(token).ConfigureAwait(false));
            string path = "/v1.0/public/feeds/" + created.Secret;
            TestResponse rss = (await anon.GetAsync(path + "/podcast.xml", token).ConfigureAwait(false)).Expect(200, "feed");
            Assert.True(rss.Headers["Content-Type"].StartsWith("application/rss+xml", StringComparison.Ordinal) && rss.Headers["X-Robots-Tag"].Contains("noindex", StringComparison.Ordinal), "feed headers");
            XDocument doc = XDocument.Parse(rss.Body);
            XNamespace itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";
            XElement item = doc.Descendants("item").Single(i => (string?)i.Element("guid") == m.Id);
            XElement enclosure = item.Element("enclosure")!;
            Assert.True(doc.Root!.Element("channel")!.Element(itunes + "image")!.Attribute("href")!.Value.EndsWith("/v1.0/public/assets/glyph.png", StringComparison.Ordinal)
                && enclosure.Attribute("length")!.Value == m.Bytes.ToString() && enclosure.Attribute("url")!.Value.Contains(created.Secret, StringComparison.Ordinal), "channel artwork and enclosure");
            Assert.True(item.Element(itunes + "duration") != null && item.Element("title")!.Value == m.Title, "item fields");

            string episodePath = new Uri(enclosure.Attribute("url")!.Value).AbsolutePath;
            TestResponse ep = (await anon.SendAsync(HttpMethod.Get, episodePath, null, new Dictionary<string, string> { { "Range", "bytes=0-9" } }, token).ConfigureAwait(false)).Expect(206, "episode range");
            Assert.True(ep.Headers["Content-Range"] == "bytes 0-9/" + m.Bytes, "episode bytes");
            (await anon.GetAsync(path + "/chapters/" + m.Id + ".json", token).ConfigureAwait(false)).Expect(200, "chapters");
            (await anon.GetAsync("/v1.0/public/feeds/mmnf_wrong/podcast.xml", token).ConfigureAwait(false)).Expect(404, "unknown token");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            FeedTokenCreated theirs = (await other.Client.PostAsync("/v1.0/api/feed-tokens", null, token).ConfigureAwait(false)).Expect(201, "other token").Json<FeedTokenCreated>();
            (await anon.GetAsync("/v1.0/public/feeds/" + theirs.Secret + "/episodes/" + m.Id + ".mp3", token).ConfigureAwait(false)).Expect(404, "another user's token cannot fetch my episode");
            Assert.True(!(await anon.GetAsync("/v1.0/public/feeds/" + theirs.Secret + "/podcast.xml", token).ConfigureAwait(false)).Body.Contains(m.Id, StringComparison.Ordinal), "another user's feed lacks my episode");

            (await f.User.Client.DeleteAsync("/v1.0/api/feed-tokens/" + created.Token.Id, token).ConfigureAwait(false)).Expect(204, "revoke");
            (await anon.GetAsync(path + "/podcast.xml", token).ConfigureAwait(false)).Expect(404, "revoked feed");
            (await anon.GetAsync(episodePath, token).ConfigureAwait(false)).Expect(404, "revoked episode");

            TestResponse glyph = (await anon.GetAsync("/v1.0/public/assets/glyph.png", token).ConfigureAwait(false)).Expect(200, "artwork");
            Assert.True(glyph.Headers["Content-Type"] == "image/png", "png");
            (await anon.GetAsync("/v1.0/public/assets/secret.txt", token).ConfigureAwait(false)).Expect(404, "only listed assets");
        }
    }
}
