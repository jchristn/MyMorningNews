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
                Cases.Case(S, "AvailableVoices", "Anyone in a workspace can list its voice profiles and the speech server's voices with readable labels, and hear a short preview", AvailableVoicesAsync),
                Cases.Case(S, "TopicVoice", "A topic's voice is validated and stored, reads the topic's briefings aloud, cannot be set by someone else, and is cleared when the voice is deleted", TopicVoiceAsync),
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

        internal static async Task<Briefing> EditionBriefingAsync(PipelineFixture f, string name, bool audio, CancellationToken token)
        {
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = name, freshResearchBeforeCompose = false, audioEnabled = audio, lookbackHours = 48 }, token).ConfigureAwait(false))
                .Expect(201, "edition").Json<EditionResponse>();
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topics");
            Briefing queued = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose").Json<Briefing>();
            Briefing b = await BriefingWaiter.FinishAsync(f.Server, queued.TenantId, queued.Id, token).ConfigureAwait(false);
            Assert.True(b.Status == BriefingStatusEnum.Ready, "briefing ready: " + b.ErrorMessage);
            return b;
        }

        internal static async Task<MediaAsset> EpisodeForAsync(PipelineFixture f, Briefing b, CancellationToken token)
        {
            // Other suites queue audio too and the queue is shared, so wait on this briefing's episode rather than counting passes.
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(60))
            {
                int done = await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, new[] { b.TenantId }, token).ConfigureAwait(false);
                EnumerationResult<MediaAsset> list = (await f.User.Client.GetAsync("/v1.0/api/media?briefingId=" + b.Id, token).ConfigureAwait(false)).Json<EnumerationResult<MediaAsset>>();
                MediaAsset? m = list.Items.OrderByDescending(x => x.CreatedUtc).FirstOrDefault();
                if (m != null && (m.Status == MediaStatusEnum.Ready || m.Status == MediaStatusEnum.Failed)) return m;
                if (done == 0) await Task.Delay(50, token).ConfigureAwait(false);
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
            TestResponse busy = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + b.Id + "/audio", null, token).ConfigureAwait(false)).Expect(409, "one at a time");
            Assert.True(busy.Body.Contains("AudioInProgress", StringComparison.Ordinal) && busy.Body.Contains(again.Id, StringComparison.Ordinal), "distinct code naming the episode: " + busy.Body);
            BriefingDetailResponse withAudio = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + b.Id, token).ConfigureAwait(false)).Json<BriefingDetailResponse>();
            Assert.True(withAudio.Audio != null && withAudio.Audio.Id == again.Id && withAudio.Audio.Status == MediaStatusEnum.Queued && withAudio.Audio.ScriptMarkdown == null, "briefing detail carries the newest audio status");
            await EpisodeForAsync(f, b, token).ConfigureAwait(false);
            (await f.User.Client.DeleteAsync("/v1.0/api/media/" + m.Id, token).ConfigureAwait(false)).Expect(204, "delete episode");
            (await f.User.Client.DeleteAsync("/v1.0/api/briefings/" + b.Id, token).ConfigureAwait(false)).Expect(204, "delete the briefing");
            Assert.Equal(0, (await f.User.Client.GetAsync("/v1.0/api/media?briefingId=" + b.Id, token).ConfigureAwait(false)).Json<EnumerationResult<MediaAsset>>().Items.Count, "its episodes go with it");
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
                await f.Server.Context.Audio.ProcessQueuedAsync(DateTime.UtcNow, new[] { queued.TenantId }, token).ConfigureAwait(false);
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

        private static async Task AvailableVoicesAsync(CancellationToken token)
        {
            Assert.True(VoiceNames.Describe("af_heart") == "Heart (American English, female)" && VoiceNames.Describe("bm_george") == "George (British English, male)"
                && VoiceNames.Describe("studio-one") == "studio-one" && VoiceNames.Describe("xx_who") == "xx_who" && VoiceNames.Describe(null) == String.Empty, "Kokoro voice IDs get readable labels; others stay as they are");

            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            AvailableVoicesResponse a = (await member.Client.GetAsync("/v1.0/api/voices/available", token).ConfigureAwait(false)).Expect(200, "available voices").Json<AvailableVoicesResponse>();
            Assert.True(a.Enabled && a.Listed && a.Voices.Count == 6, "the workspace default profile and the speech server's five other voices: " + String.Join(", ", a.Voices.Select(v => v.Voice)));
            AvailableVoice first = a.Voices[0];
            Assert.True(first.Voice == "af_heart" && first.IsDefault && first.VoiceProfileId == a.DefaultVoiceProfileId && first.Name == AudioService.DefaultVoiceName && first.Label == "Heart (American English, female)",
                "the default voice profile comes first: " + first.Voice + " " + first.Name);
            AvailableVoice emma = a.Voices.Single(v => v.Voice == "bf_emma");
            Assert.True(emma.VoiceProfileId == null && emma.Name == null && !emma.IsDefault && emma.Label == "Emma (British English, female)", "a speech server voice has a label and no profile yet");
            Assert.Equal("studio-one", a.Voices.Single(v => v.Voice == "studio-one").Label, "an unknown voice ID is its own label");

            int before = StubModelServer.Shared.SpeechRequests.Count;
            TestResponse sample = (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "bf_emma", text = new string('a', 1000) }, token).ConfigureAwait(false)).Expect(200, "preview a server voice");
            Assert.True(sample.Headers["Content-Type"].StartsWith("audio/wav", StringComparison.Ordinal) && sample.Body.StartsWith("RIFF", StringComparison.Ordinal), "preview audio");
            string spoken = StubModelServer.Shared.SpeechRequests.Skip(before).Last(r => r.Contains("bf_emma", StringComparison.Ordinal));
            Assert.True(spoken.Contains(new string('a', 300), StringComparison.Ordinal) && !spoken.Contains(new string('a', 301), StringComparison.Ordinal), "preview text is cut to 300 characters");
            (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voiceProfileId = first.VoiceProfileId }, token).ConfigureAwait(false)).Expect(200, "preview a voice profile with the default sentence");
            (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "nobody" }, token).ConfigureAwait(false)).Expect(400, "a voice the server does not offer");
            (await member.Client.PostAsync("/v1.0/api/voices/preview", new { }, token).ConfigureAwait(false)).Expect(400, "a voice is required");
            (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voiceProfileId = "vce_missing" }, token).ConfigureAwait(false)).Expect(404, "unknown voice profile");
            Assert.Equal(1L, (await member.Client.GetAsync("/v1.0/api/voices", token).ConfigureAwait(false)).Json<EnumerationResult<VoiceProfile>>().TotalCount, "listing and previewing create no voice profiles");

            // A speech server without a voice listing: the configured default voices are offered.
            string endpoint = server.Context.Settings.Tts.Endpoint;
            try
            {
                server.Context.Settings.Tts.Endpoint = StubModelServer.Shared.BaseUrl + "/novoices";
                AvailableVoicesResponse plain = (await member.Client.GetAsync("/v1.0/api/voices/available", token).ConfigureAwait(false)).Expect(200, "no listing").Json<AvailableVoicesResponse>();
                Assert.True(!plain.Listed && plain.Voices.Count == 2 && plain.Voices[0].VoiceProfileId != null && plain.Voices[1].Voice == "am_michael" && plain.Voices[1].VoiceProfileId == null,
                    "falls back to the configured voices: " + String.Join(", ", plain.Voices.Select(v => v.Voice)));
                (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "am_michael" }, token).ConfigureAwait(false)).Expect(200, "a configured voice still previews");
                (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "bf_emma" }, token).ConfigureAwait(false)).Expect(400, "an unlisted voice does not");
            }
            finally
            {
                server.Context.Settings.Tts.Endpoint = endpoint;
            }

            // Previews are limited per user.
            int last = 200;
            for (int i = 0; i < 25 && last != 429; i++)
                last = (await member.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "af_heart", text = "Hi." }, token).ConfigureAwait(false)).StatusCode;
            Assert.Equal(429, last, "more than 20 previews a minute are refused");
            TestUser other = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            (await other.Client.PostAsync("/v1.0/api/voices/preview", new { voice = "af_heart", text = "Hi." }, token).ConfigureAwait(false)).Expect(200, "the limit is per user");
        }

        private static async Task TopicVoiceAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            string topicPath = "/v1.0/api/topics/" + f.Topic.Id;
            Assert.True(f.Topic.VoiceProfileId == null, "a new topic uses the workspace default voice");
            VoiceProfile fallback = (await f.User.Client.GetAsync("/v1.0/api/voices", token).ConfigureAwait(false)).Json<EnumerationResult<VoiceProfile>>().Items.Single();

            // Validation: the profile must exist in this workspace, and a server voice must be one the server offers.
            ErrorResponse bad = (await f.User.Client.PutAsync(topicPath, new { voiceProfileId = "vce_missing" }, token).ConfigureAwait(false)).Expect(400, "unknown voice profile").Json<ErrorResponse>();
            Assert.True(bad.Message.Contains("voiceProfileId", StringComparison.Ordinal), "names the field: " + bad.Message);
            Tenant elsewhere = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser outsider = await TestFixtures.CreateUserAsync(admin, elsewhere.Id, true, token).ConfigureAwait(false);
            VoiceProfile theirs = (await outsider.Client.PostAsync("/v1.0/api/voices", new { name = "Theirs", voice = "af_bella" }, token).ConfigureAwait(false)).Expect(201, "another workspace's voice").Json<VoiceProfile>();
            (await f.User.Client.PutAsync(topicPath, new { voiceProfileId = theirs.Id }, token).ConfigureAwait(false)).Expect(400, "another workspace's voice profile");
            (await f.User.Client.PutAsync(topicPath, new { voice = "nobody" }, token).ConfigureAwait(false)).Expect(400, "a voice the speech server does not offer");
            (await f.User.Client.PostAsync("/v1.0/api/topics", new { name = "Bad voice " + Guid.NewGuid().ToString("N").Substring(0, 6), voice = "nobody" }, token).ConfigureAwait(false)).Expect(400, "create validates too");
            Assert.Equal(1L, (await f.User.Client.GetAsync("/v1.0/api/topics", token).ConfigureAwait(false)).Json<EnumerationResult<Topic>>().TotalCount, "a refused create leaves no topic behind");

            // Someone who cannot edit the topic cannot set its voice.
            TestUser member = await TestFixtures.CreateUserAsync(admin, f.User.User.TenantId, false, token).ConfigureAwait(false);
            (await member.Client.PutAsync(topicPath, new { voice = "bf_emma" }, token).ConfigureAwait(false)).Expect(404, "another member's topic");
            (await outsider.Client.PutAsync(topicPath, new { voice = "bf_emma" }, token).ConfigureAwait(false)).Expect(404, "another workspace's topic");
            Assert.Equal(1L, (await f.User.Client.GetAsync("/v1.0/api/voices", token).ConfigureAwait(false)).Json<EnumerationResult<VoiceProfile>>().TotalCount, "refused requests create no voice profiles");

            // Choosing a speech server voice creates a profile for it once and reuses it afterwards.
            Topic chosen = (await f.User.Client.PutAsync(topicPath, new { voice = "bf_emma" }, token).ConfigureAwait(false)).Expect(200, "choose a server voice").Json<Topic>();
            VoiceProfile emma = (await f.User.Client.GetAsync("/v1.0/api/voices/" + chosen.VoiceProfileId, token).ConfigureAwait(false)).Expect(200, "its profile").Json<VoiceProfile>();
            Assert.True(emma.Voice == "bf_emma" && emma.Name == "Emma (British English, female)" && !emma.IsDefault && emma.Endpoint == null && emma.Model == "kokoro" && emma.SecondaryVoice == "am_michael",
                "a profile named after the voice: " + emma.Name);
            Topic second = (await f.User.Client.PostAsync("/v1.0/api/topics", new { name = "Second " + Guid.NewGuid().ToString("N").Substring(0, 6), voice = "bf_emma" }, token).ConfigureAwait(false)).Expect(201, "create with a voice").Json<Topic>();
            Assert.Equal(emma.Id, second.VoiceProfileId, "the same voice reuses its profile");
            Assert.Equal(2L, (await f.User.Client.GetAsync("/v1.0/api/voices", token).ConfigureAwait(false)).Json<EnumerationResult<VoiceProfile>>().TotalCount, "one profile per voice");
            AvailableVoicesResponse available = (await f.User.Client.GetAsync("/v1.0/api/voices/available", token).ConfigureAwait(false)).Json<AvailableVoicesResponse>();
            Assert.True(available.Voices.Count(v => v.Voice == "bf_emma") == 1 && available.Voices.Single(v => v.Voice == "bf_emma").VoiceProfileId == emma.Id, "the voice is now listed once, as a profile");
            Topic copy = (await f.User.Client.PostAsync(topicPath + "/duplicate", null, token).ConfigureAwait(false)).Expect(201, "duplicate").Json<TopicDetailResponse>().Topic;
            Assert.Equal(emma.Id, copy.VoiceProfileId, "a copy keeps the voice");
            Topic renamed = (await f.User.Client.PutAsync(topicPath, new { noveltyPreference = 60 }, token).ConfigureAwait(false)).Expect(200, "another edit").Json<Topic>();
            Assert.Equal(emma.Id, renamed.VoiceProfileId, "an edit that does not name the voice leaves it");
            TopicDetailResponse detail = (await f.User.Client.GetAsync(topicPath, token).ConfigureAwait(false)).Expect(200, "read").Json<TopicDetailResponse>();
            Assert.Equal(emma.Id, detail.Topic.VoiceProfileId, "stored");

            // The topic's briefing is read in the topic's voice; an explicit voice and an edition's voice still win.
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Run run = (await f.User.Client.GetAsync("/v1.0/api/runs?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Run>>().Items.First();
            Briefing runBriefing = (await f.User.Client.GetAsync("/v1.0/api/briefings?runId=" + run.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().Items.Single();
            MediaAsset queued = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + runBriefing.Id + "/audio", null, token).ConfigureAwait(false)).Expect(202, "audio for the run briefing").Json<MediaAsset>();
            Assert.Equal(emma.Id, queued.VoiceProfileId, "queued with the topic's voice");
            int before = StubModelServer.Shared.SpeechRequests.Count;
            MediaAsset made = await EpisodeForAsync(f, runBriefing, token).ConfigureAwait(false);
            Assert.True(made.Status == MediaStatusEnum.Ready && made.VoiceProfileId == emma.Id, "episode made: " + made.Status + " " + made.ErrorMessage);
            Assert.True(StubModelServer.Shared.SpeechRequests.Skip(before).Any(r => r.Contains("\"bf_emma\"", StringComparison.Ordinal)), "spoken in the topic's voice");
            MediaAsset explicitVoice = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + runBriefing.Id + "/audio", new { voiceProfileId = fallback.Id }, token).ConfigureAwait(false)).Expect(202, "remake with a named voice").Json<MediaAsset>();
            Assert.Equal(fallback.Id, explicitVoice.VoiceProfileId, "a voice named in the request wins");
            await EpisodeForAsync(f, runBriefing, token).ConfigureAwait(false);
            Briefing editionBriefing = await EditionBriefingAsync(f, "Voice Heat", true, token).ConfigureAwait(false);
            MediaAsset editionAudio = (await f.User.Client.GetAsync("/v1.0/api/media?briefingId=" + editionBriefing.Id, token).ConfigureAwait(false)).Json<EnumerationResult<MediaAsset>>().Items.Single();
            Assert.Equal(fallback.Id, editionAudio.VoiceProfileId, "an edition keeps its own voice (here the workspace default)");
            await EpisodeForAsync(f, editionBriefing, token).ConfigureAwait(false);

            // Clearing, and deleting the voice.
            Topic cleared = (await f.User.Client.PutAsync("/v1.0/api/topics/" + second.Id, new { voiceProfileId = String.Empty }, token).ConfigureAwait(false)).Expect(200, "clear").Json<Topic>();
            Assert.True(cleared.VoiceProfileId == null, "an empty string goes back to the workspace default");
            Topic byProfile = (await f.User.Client.PutAsync("/v1.0/api/topics/" + second.Id, new { voiceProfileId = emma.Id }, token).ConfigureAwait(false)).Expect(200, "set by profile").Json<Topic>();
            Assert.Equal(emma.Id, byProfile.VoiceProfileId, "set by voice profile ID");
            (await f.User.Client.DeleteAsync("/v1.0/api/voices/" + emma.Id, token).ConfigureAwait(false)).Expect(204, "delete the voice");
            foreach (string id in new[] { f.Topic.Id, second.Id, copy.Id })
            {
                Topic after = (await f.User.Client.GetAsync("/v1.0/api/topics/" + id, token).ConfigureAwait(false)).Expect(200, "read after delete").Json<TopicDetailResponse>().Topic;
                Assert.True(after.VoiceProfileId == null, "deleting a voice clears it from topic " + id);
            }

            MediaAsset afterDelete = (await f.User.Client.PostAsync("/v1.0/api/briefings/" + runBriefing.Id + "/audio", null, token).ConfigureAwait(false)).Expect(202, "audio after the voice is gone").Json<MediaAsset>();
            Assert.Equal(fallback.Id, afterDelete.VoiceProfileId, "the workspace default reads it again");
            await EpisodeForAsync(f, runBriefing, token).ConfigureAwait(false);
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
            Assert.True((await f.User.Client.GetAsync("/v1.0/api/feed-tokens", token).ConfigureAwait(false)).Json<List<FeedToken>>().Single().RevokedUtc.HasValue, "revoked token listed");
            (await anon.GetAsync(path + "/podcast.xml", token).ConfigureAwait(false)).Expect(404, "revoked feed");
            (await anon.GetAsync(episodePath, token).ConfigureAwait(false)).Expect(404, "revoked episode");
            (await f.User.Client.DeleteAsync("/v1.0/api/feed-tokens/" + created.Token.Id + "?purge=true", token).ConfigureAwait(false)).Expect(204, "purge revoked token");
            Assert.Equal(0, (await f.User.Client.GetAsync("/v1.0/api/feed-tokens", token).ConfigureAwait(false)).Json<List<FeedToken>>().Count, "purged");
            ClientConfigResponse cfg = (await anon.GetAsync("/v1.0/api/client-config", token).ConfigureAwait(false)).Expect(200, "client config").Json<ClientConfigResponse>();
            Assert.True(cfg.AudioEnabled && cfg.EmailEnabled, "client config reports audio and email");

            TestResponse glyph = (await anon.GetAsync("/v1.0/public/assets/glyph.png", token).ConfigureAwait(false)).Expect(200, "artwork");
            Assert.True(glyph.Headers["Content-Type"] == "image/png", "png");
            (await anon.GetAsync("/v1.0/public/assets/secret.txt", token).ConfigureAwait(false)).Expect(404, "only listed assets");
        }
    }
}
