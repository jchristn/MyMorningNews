namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Briefings, templates, and editions (plan section 21): the template format, safe rendering, run briefings with
    /// citations and files, editions with fresh research and schedules, model fallbacks, pinning, and regeneration.
    /// </summary>
    public static class BriefingSuite
    {
        private const string S = "Briefings";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Briefings, templates, and editions", new[]
            {
                Cases.Case(S, "TemplateFormat", "Default templates parse; errors and warnings carry line numbers", TemplateFormatAsync, "fast"),
                Cases.Case(S, "SafeMarkdown", "Markdown renders to HTML without letting markup or unsafe links through", SafeMarkdownAsync, "fast"),
                Cases.Case(S, "RunBriefing", "A run composes a briefing with sections, numbered citations, items, and Markdown and HTML files", RunBriefingAsync),
                Cases.Case(S, "TemplatesApi", "Templates are listed, validated, created, versioned, rolled back, reset, exported, and protected", TemplatesApiAsync),
                Cases.Case(S, "EditionCompose", "An edition composes from its template; a failed section falls back to a plain list; previews save nothing", EditionComposeAsync),
                Cases.Case(S, "FreshResearch", "Fresh research runs first and the briefing waits for it", FreshResearchAsync),
                Cases.Case(S, "EditionSchedule", "New users get Morning News, and a due edition schedule queues a briefing", EditionScheduleAsync),
                Cases.Case(S, "PinRegenerateDelete", "Briefings pin within the cap, regenerate expired files, and delete with their files", PinRegenerateDeleteAsync)
            });
        }

        private static Task TemplateFormatAsync(CancellationToken token)
        {
            foreach (KeyValuePair<string, string> d in DefaultTemplateCatalog.All())
            {
                BriefingTemplateDefinition def = TemplateParser.Parse(d.Value);
                Assert.True(!TemplateParser.HasErrors(def), d.Key + " parses: " + String.Join("; ", def.Problems.Select(p => p.Line + " " + p.Message)));
                Assert.True(def.Sections.Count >= 4 && def.Sections.Last().Directive.Source == "citations", d.Key + " sections");
            }

            BriefingTemplateDefinition mn = TemplateParser.Parse(DefaultTemplateCatalog.Get(DefaultTemplateCatalog.MorningNews));
            Assert.True(mn.Name == "Morning News" && mn.Audio.Enabled && mn.Audio.Format == "two-host" && mn.Audio.TargetMinutes == 8 && mn.Audio.Intro!.StartsWith("Good morning", StringComparison.Ordinal), "front matter");
            TemplateSection top = mn.Sections[0];
            Assert.True(top.Key == "top-of-the-news" && top.Directive.Source == "findings" && top.Directive.Tier == "MustRead" && top.Directive.Limit == 5 && top.Instruction.StartsWith("Lead with", StringComparison.Ordinal), "directive and instruction");
            Assert.True(mn.Sections[1].Directive.Group == "topic" && mn.Title == "{{edition.name}} for {{date.long}}" && mn.OpeningInstruction.Length > 0, "group, title, opening");

            string bad = "---\nname: Bad\nlength: huge\ncolor: blue\n---\n# {{edition.nam}}\n\n## One\n<!-- mmn:section source=findings limit=99 flavor=x -->\n\n## Two\n<!-- mmn:section source=predictions status=later -->\n<!-- mmn:sectoin source=x -->\n";
            BriefingTemplateDefinition b = TemplateParser.Parse(bad);
            Assert.True(TemplateParser.HasErrors(b), "bad template has errors");
            Dictionary<int, string> byLine = b.Problems.Where(p => !p.IsWarning).GroupBy(p => p.Line).ToDictionary(g => g.Key, g => String.Join(" | ", g.Select(p => p.Message)));
            Assert.True(byLine.ContainsKey(3) && byLine[3].Contains("length", StringComparison.Ordinal), "line 3 length");
            Assert.True(byLine.ContainsKey(4) && byLine[4].Contains("color", StringComparison.Ordinal), "line 4 unknown option");
            Assert.True(byLine.ContainsKey(6) && byLine[6].Contains("edition.nam", StringComparison.Ordinal), "line 6 unknown variable");
            Assert.True(byLine.ContainsKey(9) && byLine[9].Contains("limit", StringComparison.Ordinal) && byLine[9].Contains("flavor", StringComparison.Ordinal), "line 9 limit and attribute: " + byLine.GetValueOrDefault(9));
            Assert.True(byLine.ContainsKey(12) && byLine[12].Contains("status", StringComparison.Ordinal), "line 12 status");
            Assert.True(byLine.ContainsKey(13), "line 13 misspelled directive");
            Assert.True(TemplateParser.Parse("## Only\nFree text.").Problems.All(p => p.IsWarning), "minimal template has only warnings");
            Assert.True(TemplateParser.HasErrors(TemplateParser.Parse("")) && TemplateParser.HasErrors(TemplateParser.Parse("# Title only")), "empty and no-section templates fail");
            Assert.Equal("top-of-the-news", TemplateParser.Slug("Top of the news!"), "slug");
            return Task.CompletedTask;
        }

        private static Task SafeMarkdownAsync(CancellationToken token)
        {
            string html = SafeMarkdown.ToHtml("# Title\n\nHello **bold** and *it* and `x<y`.\n\n- [ok](https://example.com/a?b=1&c=2)\n- [bad](javascript:void)\n- [worse](javascript:alert(1))\n\n<script>alert(1)</script>\n\n> quoted\n\n1. one\n2. two\n\n---");
            Assert.True(html.Contains("<h1>Title</h1>", StringComparison.Ordinal) && html.Contains("<strong>bold</strong>", StringComparison.Ordinal) && html.Contains("<em>it</em>", StringComparison.Ordinal), "inline: " + html);
            Assert.True(html.Contains("<code>x&lt;y</code>", StringComparison.Ordinal), "code encoded");
            Assert.True(html.Contains("<a href=\"https://example.com/a?b=1&amp;c=2\">ok</a>", StringComparison.Ordinal), "safe link");
            Assert.True(!html.Contains("href=\"javascript", StringComparison.OrdinalIgnoreCase) && html.Contains("<li>bad</li>", StringComparison.Ordinal), "unsafe links never become links: " + html);
            Assert.True(!html.Contains("<script>", StringComparison.Ordinal) && html.Contains("&lt;script&gt;", StringComparison.Ordinal), "raw HTML shown as text");
            Assert.True(html.Contains("<blockquote><p>quoted</p>", StringComparison.Ordinal) && html.Contains("<ol>", StringComparison.Ordinal) && html.Contains("<hr>", StringComparison.Ordinal), "blocks");
            Assert.True(SafeMarkdown.ToHtml("# A", 1).Contains("<h2>A</h2>", StringComparison.Ordinal), "heading offset");
            string page = BriefingHtmlRenderer.Render("T <x>", "# T\n\nBody", "Nov 19, 2026");
            Assert.True(page.Contains("<title>T &lt;x&gt;</title>", StringComparison.Ordinal) && page.Contains("prefers-color-scheme:dark", StringComparison.Ordinal) && page.Contains("© 2026 Lattice AI LLC", StringComparison.Ordinal)
                && page.Contains("class=\"meta\"", StringComparison.Ordinal), "report page");
            return Task.CompletedTask;
        }

        private static async Task<Briefing> RunBriefingForAsync(PipelineSuite.PipelineFixture f, Run run, CancellationToken token)
        {
            EnumerationResult<Briefing> list = (await f.User.Client.GetAsync("/v1.0/api/briefings?runId=" + run.Id, token).ConfigureAwait(false)).Expect(200, "briefings by run").Json<EnumerationResult<Briefing>>();
            Assert.Equal(1, list.Items.Count, "one briefing for the run");
            return list.Items[0];
        }

        private static async Task RunBriefingAsync(CancellationToken token)
        {
            PipelineSuite.PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Assert.True(run.Status == RunStatusEnum.Completed, "run completed: " + run.Status + " " + run.Summary);
            Briefing listed = await RunBriefingForAsync(f, run, token).ConfigureAwait(false);
            Assert.True(listed.Kind == BriefingKindEnum.Run && listed.Status == BriefingStatusEnum.Ready && listed.Markdown == null && listed.ItemCount >= 3 && listed.ModelFallbacks == 0, "listed without Markdown");

            BriefingDetailResponse d = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + listed.Id, token).ConfigureAwait(false)).Expect(200, "detail").Json<BriefingDetailResponse>();
            string md = d.Briefing.Markdown!;
            Assert.True(md.StartsWith("# " + f.Topic.Name + ": ", StringComparison.Ordinal), "title from the run template: " + md.Split('\n')[0]);
            Assert.True(md.Contains("Rebates moved today.", StringComparison.Ordinal), "model opening");
            Assert.True(md.Contains("## What is new\n\nWritten What is new section citing [1].", StringComparison.Ordinal), "section body with the repeated heading removed");
            Assert.True(md.Contains("## Sources\n\n1. [", StringComparison.Ordinal) && md.Contains("/news/a.html)", StringComparison.Ordinal) && md.Contains("2. [Efficiency standards tighten]", StringComparison.Ordinal), "numbered citations resolve: " + md);
            Assert.True(d.Sections.First().SectionKey == "opening" && d.Sections.Any(s => s.SectionKey == "what-is-new") && d.Sections.Any(s => s.Source == "citations"), "sections saved");
            List<BriefingItem> findings = d.Items.Where(i => i.ItemType == BriefingItemTypeEnum.Finding).ToList();
            Assert.True(findings.Count == 2 && findings.Select(i => i.CitationNumber).OrderBy(n => n).SequenceEqual(new int?[] { 1, 2 }), "finding items with citation numbers");
            Assert.True(d.Items.Any(i => i.ItemType == BriefingItemTypeEnum.Prediction) && d.Items.Any(i => i.ItemType == BriefingItemTypeEnum.Suggestion), "prediction and suggestion items");
            Assert.True(d.Files.Count == 2 && !d.FilesExpired && d.Files.All(a => a.ExpiresUtc > DateTime.UtcNow.AddDays(364)), "two files kept 365 days");
            Assert.True(d.TopicName == f.Topic.Name && d.Briefing.WordCount > 10 && d.Briefing.ReadMinutes >= 1, "topic name and counts");

            TestResponse mdFile = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + listed.Id + "/download?format=md", token).ConfigureAwait(false)).Expect(200, "markdown download");
            Assert.True(mdFile.Body == md && mdFile.Headers["Content-Type"].StartsWith("text/markdown", StringComparison.Ordinal) && mdFile.Headers["Content-Disposition"].Contains(".md", StringComparison.Ordinal), "markdown file");
            TestResponse html = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + listed.Id + "/download?format=html", token).ConfigureAwait(false)).Expect(200, "html download");
            Assert.True(html.Body.Contains("<h2>Sources</h2>", StringComparison.Ordinal) && html.Body.Contains("min read", StringComparison.Ordinal) && html.Body.Contains("© 2026 Lattice AI LLC", StringComparison.Ordinal), "html report");
            (await f.User.Client.GetAsync("/v1.0/api/briefings/" + listed.Id + "/download?format=pdf", token).ConfigureAwait(false)).Expect(400, "unknown format");
            EnumerationResult<LlmCall> calls = (await f.User.Client.GetAsync("/v1.0/api/llm-calls?pageSize=200", token).ConfigureAwait(false)).Json<EnumerationResult<LlmCall>>();
            Assert.True(calls.Items.Count(c => c.BriefingId == listed.Id) >= 3 && calls.Items.Any(c => c.BriefingId == listed.Id && c.PromptKey == "briefing.compose"), "composition calls attributed to the briefing");

            TestUser other = await TestFixtures.CreateUserAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false), f.Topic.TenantId, false, token).ConfigureAwait(false);
            (await other.Client.GetAsync("/v1.0/api/briefings/" + listed.Id, token).ConfigureAwait(false)).Expect(404, "another member cannot read it");
            Assert.Equal(0, (await other.Client.GetAsync("/v1.0/api/briefings", token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().Items.Count, "another member lists none");
        }

        private static async Task TemplatesApiAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            EnumerationResult<BriefingTemplate> list = (await member.Client.GetAsync("/v1.0/api/briefing-templates", token).ConfigureAwait(false)).Expect(200, "list").Json<EnumerationResult<BriefingTemplate>>();
            Assert.True(list.Items.Count == 3 && list.Items.All(x => x.UserId == null && x.DefaultKey != null) && list.Items.Count(x => x.IsDefault) == 1, "three shared defaults");
            BriefingTemplate morning = list.Items.Single(x => x.DefaultKey == "morning-news");

            TemplateValidationResponse v = (await member.Client.PostAsync("/v1.0/api/briefing-templates/validate", new { content = "## A\n<!-- mmn:section source=nope -->" }, token).ConfigureAwait(false))
                .Expect(200, "validate").Json<TemplateValidationResponse>();
            Assert.True(!v.Valid && v.Problems.Any(p => p.Line == 2 && !p.IsWarning), "validation problems with lines");
            TestResponse badCreate = (await member.Client.PostAsync("/v1.0/api/briefing-templates", new { content = "## A\n<!-- mmn:section limit=0 -->" }, token).ConfigureAwait(false)).Expect(400, "invalid create");
            Assert.True(badCreate.Body.Contains("TemplateInvalid", StringComparison.Ordinal) && badCreate.Body.Contains("line 2", StringComparison.Ordinal), "error with line details: " + badCreate.Body);

            string mine = "---\nname: My Brief\ntone: warm\n---\n# {{topic.name}}\n\n## Highlights\n<!-- mmn:section source=findings limit=3 -->\nShort.\n";
            TemplateDetailResponse created = (await member.Client.PostAsync("/v1.0/api/briefing-templates", new { content = mine }, token).ConfigureAwait(false)).Expect(201, "create").Json<TemplateDetailResponse>();
            Assert.True(created.Template.Name == "My Brief" && created.Template.UserId == member.User.Id && !created.Shared && created.Sections.Count == 1, "personal template");
            (await member.Client.PostAsync("/v1.0/api/briefing-templates", new { content = mine }, token).ConfigureAwait(false)).Expect(409, "duplicate name");
            TemplateDetailResponse saved = (await member.Client.PutAsync("/v1.0/api/briefing-templates/" + created.Template.Id, new { content = mine.Replace("limit=3", "limit=4"), changeNote = "More" }, token).ConfigureAwait(false))
                .Expect(200, "save").Json<TemplateDetailResponse>();
            Assert.True(saved.Template.CurrentVersion == 2 && saved.Sections[0].Directive.Limit == 4, "version 2");
            List<BriefingTemplateVersion> versions = (await member.Client.GetAsync("/v1.0/api/briefing-templates/" + created.Template.Id + "/versions", token).ConfigureAwait(false)).Json<List<BriefingTemplateVersion>>();
            Assert.True(versions.Count == 2 && versions[0].Version == 2 && versions[0].ChangeNote == "More", "versions newest first");
            TemplateDetailResponse rolled = (await member.Client.PostAsync("/v1.0/api/briefing-templates/" + created.Template.Id + "/rollback", new { version = 1 }, token).ConfigureAwait(false))
                .Expect(200, "rollback").Json<TemplateDetailResponse>();
            Assert.True(rolled.Template.CurrentVersion == 3 && rolled.Sections[0].Directive.Limit == 3, "rolled back as version 3");
            TestResponse export = (await member.Client.GetAsync("/v1.0/api/briefing-templates/" + created.Template.Id + "/export", token).ConfigureAwait(false)).Expect(200, "export");
            Assert.True(export.Body == mine && export.Headers["Content-Disposition"].Contains("my-brief.md", StringComparison.Ordinal), "export file");
            (await ta.Client.GetAsync("/v1.0/api/briefing-templates/" + created.Template.Id, token).ConfigureAwait(false)).Expect(200, "tenant admin can read it");

            (await member.Client.PutAsync("/v1.0/api/briefing-templates/" + morning.Id, new { content = mine }, token).ConfigureAwait(false)).Expect(403, "members cannot change shared templates");
            (await member.Client.PostAsync("/v1.0/api/briefing-templates", new { content = mine.Replace("My Brief", "Shared One"), shared = true }, token).ConfigureAwait(false)).Expect(403, "members cannot create shared templates");
            string edited = DefaultTemplateCatalog.Get("morning-news").Replace("limit=5 order=relevance", "limit=3 order=relevance");
            TemplateDetailResponse customized = (await ta.Client.PutAsync("/v1.0/api/briefing-templates/" + morning.Id, new { content = edited }, token).ConfigureAwait(false)).Expect(200, "admin edits shared").Json<TemplateDetailResponse>();
            Assert.True(customized.Template.Customized && customized.Sections[0].Directive.Limit == 3, "customized");
            TemplateDetailResponse reset = (await ta.Client.PostAsync("/v1.0/api/briefing-templates/" + morning.Id + "/reset", null, token).ConfigureAwait(false)).Expect(200, "reset").Json<TemplateDetailResponse>();
            Assert.True(!reset.Template.Customized && reset.Content == DefaultTemplateCatalog.Get("morning-news"), "reset to default");
            (await ta.Client.DeleteAsync("/v1.0/api/briefing-templates/" + morning.Id, token).ConfigureAwait(false)).Expect(409, "defaults cannot be deleted");
            (await member.Client.DeleteAsync("/v1.0/api/briefing-templates/" + created.Template.Id, token).ConfigureAwait(false)).Expect(204, "delete mine");
            (await member.Client.GetAsync("/v1.0/api/briefing-templates/" + created.Template.Id, token).ConfigureAwait(false)).Expect(404, "deleted");
        }

        private static async Task<EditionResponse> CreateEditionAsync(PipelineSuite.PipelineFixture f, string name, bool fresh, string? templateId, CancellationToken token)
        {
            EditionResponse e = (await f.User.Client.PostAsync("/v1.0/api/editions", new { name = name, freshResearchBeforeCompose = fresh, lookbackHours = 48, briefingTemplateId = templateId }, token).ConfigureAwait(false))
                .Expect(201, "create edition").Json<EditionResponse>();
            return (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { f.Topic.Id } }, token).ConfigureAwait(false)).Expect(200, "topics").Json<EditionResponse>();
        }

        private static async Task<Briefing> ComposeNowAsync(PipelineSuite.PipelineFixture f, string editionId, CancellationToken token)
        {
            Briefing queued = (await f.User.Client.PostAsync("/v1.0/api/editions/" + editionId + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose").Json<Briefing>();
            Assert.True(queued.Status == BriefingStatusEnum.Queued && queued.Kind == BriefingKindEnum.Edition, "queued");
            await f.Server.Context.Briefings.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            return (await f.Server.Context.Database.Briefings.ReadAsync(queued.TenantId, queued.Id, token).ConfigureAwait(false))!;
        }

        private static async Task EditionComposeAsync(CancellationToken token)
        {
            PipelineSuite.PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EditionResponse e = await CreateEditionAsync(f, "Weekday Heat", false, null, token).ConfigureAwait(false);
            Assert.True(e.Topics.Count == 1 && e.CoveredTopicIds.SequenceEqual(new[] { f.Topic.Id }), "topics set");
            (await f.User.Client.PutAsync("/v1.0/api/editions/" + e.Edition.Id + "/topics", new { topicIds = new[] { "top_missing" } }, token).ConfigureAwait(false)).Expect(400, "unknown topic");

            Briefing b = await ComposeNowAsync(f, e.Edition.Id, token).ConfigureAwait(false);
            Assert.True(b.Status == BriefingStatusEnum.Ready && b.EditionId == e.Edition.Id && b.ModelFallbacks == 0, "composed: " + b.Status + " " + b.ErrorMessage);
            Assert.True(b.Markdown!.StartsWith("# Weekday Heat for ", StringComparison.Ordinal) && !b.Markdown.Contains("## Top of the news", StringComparison.Ordinal) && b.Markdown.Contains("## By topic", StringComparison.Ordinal)
                && b.Markdown.Contains("## Sources", StringComparison.Ordinal), "morning news template, empty sections left out: " + b.Markdown);
            Edition after = (await f.User.Client.GetAsync("/v1.0/api/editions/" + e.Edition.Id, token).ConfigureAwait(false)).Json<EditionResponse>().Edition;
            Assert.True(after.LastComposedUtc.HasValue, "last composed recorded");
            (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/compose", null, token).ConfigureAwait(false)).Expect(202, "compose again after the first finished");
            (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/compose", null, token).ConfigureAwait(false)).Expect(409, "one at a time");
            await f.Server.Context.Briefings.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);

            string failing = "---\nname: Fallback Test\n---\n# {{edition.name}}\n\n## Highlights\n<!-- mmn:section source=findings tier=Fyi limit=5 -->\nFAILSECTION please.\n\n## Sources\n<!-- mmn:section source=citations -->\n";
            TemplateDetailResponse tpl = (await f.User.Client.PostAsync("/v1.0/api/briefing-templates", new { content = failing }, token).ConfigureAwait(false)).Expect(201, "template").Json<TemplateDetailResponse>();
            EditionResponse e2 = await CreateEditionAsync(f, "Fallback Edition", false, tpl.Template.Id, token).ConfigureAwait(false);
            Briefing fb = await ComposeNowAsync(f, e2.Edition.Id, token).ConfigureAwait(false);
            Assert.True(fb.Status == BriefingStatusEnum.Ready && fb.ModelFallbacks == 1 && fb.Markdown!.Contains("## Highlights\n\n- **Heat pump rebates expand** [1]", StringComparison.Ordinal), "plain fallback: " + fb.Markdown);

            long before = (await f.User.Client.GetAsync("/v1.0/api/briefings?editionId=" + e.Edition.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().TotalCount;
            Briefing preview = (await f.User.Client.PostAsync("/v1.0/api/editions/" + e.Edition.Id + "/preview", null, token).ConfigureAwait(false)).Expect(200, "preview").Json<Briefing>();
            Assert.True(preview.Markdown!.StartsWith("# Weekday Heat for ", StringComparison.Ordinal) && !preview.Markdown.Contains("## By topic", StringComparison.Ordinal) && preview.Markdown.Contains("## Dig into next", StringComparison.Ordinal),
                "preview: findings only since the last edition, open suggestions still listed: " + preview.Markdown);
            (await f.User.Client.GetAsync("/v1.0/api/briefings/" + preview.Id, token).ConfigureAwait(false)).Expect(404, "preview not saved");
            long afterCount = (await f.User.Client.GetAsync("/v1.0/api/briefings?editionId=" + e.Edition.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>().TotalCount;
            Assert.Equal(before, afterCount, "preview saved nothing");
            (await f.User.Client.DeleteAsync("/v1.0/api/editions/" + e2.Edition.Id, token).ConfigureAwait(false)).Expect(204, "delete edition");
            Assert.True((await f.Server.Context.Database.Briefings.ReadAsync(fb.TenantId, fb.Id, token).ConfigureAwait(false)) != null, "its briefings stay");
        }

        private static async Task FreshResearchAsync(CancellationToken token)
        {
            PipelineSuite.PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            EditionResponse e = await CreateEditionAsync(f, "Fresh Heat", true, null, token).ConfigureAwait(false);
            Briefing waiting = await ComposeNowAsync(f, e.Edition.Id, token).ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            Assert.True(waiting.Status == BriefingStatusEnum.Queued && waiting.ResearchDeadlineUtc > now.AddMinutes(30) && waiting.ComposeAfterUtc > now.AddSeconds(10), "put back to wait for research: " + waiting.Status + " " + waiting.ComposeAfterUtc);
            EnumerationResult<Run> runs = (await f.User.Client.GetAsync("/v1.0/api/runs?topicId=" + f.Topic.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Run>>();
            Assert.True(runs.Items.Count == 1 && runs.Items[0].Trigger == RunTriggerEnum.Edition && runs.Items[0].Status == RunStatusEnum.Queued, "fresh research queued");
            for (int i = 0; i < 10; i++)
            {
                await f.Server.Context.Scheduling.ExecuteQueuedAsync(f.Server.Context.RunExecutor, "test", token).ConfigureAwait(false);
                Run r = (await f.Server.Context.Database.Runs.ReadAsync(f.Topic.TenantId, runs.Items[0].Id, token).ConfigureAwait(false))!;
                if (r.Status != RunStatusEnum.Queued && r.Status != RunStatusEnum.Running) break;
            }

            await f.Server.Context.Briefings.ProcessQueuedAsync(now.AddMinutes(1), token).ConfigureAwait(false);
            Briefing done = (await f.Server.Context.Database.Briefings.ReadAsync(waiting.TenantId, waiting.Id, token).ConfigureAwait(false))!;
            Assert.True(done.Status == BriefingStatusEnum.Ready && done.Markdown!.Contains("Heat pump rebates expand", StringComparison.Ordinal), "composed with the fresh findings: " + done.Status + " " + done.ErrorMessage);
        }

        private static async Task EditionScheduleAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            EnumerationResult<Edition> mine = (await u.Client.GetAsync("/v1.0/api/editions", token).ConfigureAwait(false)).Expect(200, "editions").Json<EnumerationResult<Edition>>();
            Assert.True(mine.Items.Count == 1 && mine.Items[0].Name == "Morning News", "Morning News seeded for a new user");
            EditionResponse mn = (await u.Client.GetAsync("/v1.0/api/editions/" + mine.Items[0].Id, token).ConfigureAwait(false)).Json<EditionResponse>();
            Assert.True(mn.Schedules.Count == 1 && mn.Schedules[0].DaysOfWeekMask == 127 && mn.Schedules[0].TimesOfDay.SequenceEqual(new[] { "06:30" }) && mn.Schedules[0].NextRunUtc.HasValue && mn.Topics.Count == 0,
                "daily 06:30 schedule covering all topics");

            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Schedule s = mn.Schedules[0];
            s.NextRunUtc = DateTime.UtcNow.AddMinutes(-1);
            await server.Context.Database.Schedules.UpdateAsync(s, token).ConfigureAwait(false);
            await server.Context.Scheduling.TickAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            EnumerationResult<Briefing> queued = (await u.Client.GetAsync("/v1.0/api/briefings?editionId=" + mn.Edition.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Briefing>>();
            Assert.True(queued.Items.Count == 1 && queued.Items[0].ScheduleId == s.Id && queued.Items[0].Status == BriefingStatusEnum.Queued, "schedule queued a briefing");
            Schedule next = (await server.Context.Database.Schedules.ReadAsync(t.Id, s.Id, token).ConfigureAwait(false))!;
            Assert.True(next.NextRunUtc > DateTime.UtcNow, "schedule advanced");

            await server.Context.Briefings.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
            Briefing empty = (await server.Context.Database.Briefings.ReadAsync(t.Id, queued.Items[0].Id, token).ConfigureAwait(false))!;
            Assert.True(empty.Status == BriefingStatusEnum.Ready && empty.ItemCount == 0 && empty.Markdown!.Contains("Nothing new in this period", StringComparison.Ordinal), "an empty edition still arrives: " + empty.Markdown);

            Schedule created = (await u.Client.PostAsync("/v1.0/api/schedules", new { ownerType = "Edition", ownerId = mn.Edition.Id, intervalType = "EveryNHours", intervalValue = 12 }, token).ConfigureAwait(false))
                .Expect(201, "edition schedule via the schedules API").Json<Schedule>();
            Assert.True(created.OwnerType == ScheduleOwnerTypeEnum.Edition && created.UserId == u.User.Id, "edition schedule owner");
            TestUser other = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            (await other.Client.PostAsync("/v1.0/api/schedules", new { ownerType = "Edition", ownerId = mn.Edition.Id, intervalType = "Manual" }, token).ConfigureAwait(false)).Expect(404, "not someone else's edition");
            (await other.Client.GetAsync("/v1.0/api/editions/" + mn.Edition.Id, token).ConfigureAwait(false)).Expect(404, "edition private");
        }

        private static async Task PinRegenerateDeleteAsync(CancellationToken token)
        {
            PipelineSuite.PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Briefing b = await RunBriefingForAsync(f, run, token).ConfigureAwait(false);
            Briefing pinned = (await f.User.Client.PutAsync("/v1.0/api/briefings/" + b.Id + "/pin", new { pinned = true }, token).ConfigureAwait(false)).Expect(200, "pin").Json<Briefing>();
            Assert.True(pinned.Pinned, "pinned");
            EnumerationResult<Artifact> files = (await f.User.Client.GetAsync("/v1.0/api/artifacts?briefingId=" + b.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Artifact>>();
            Assert.True(files.Items.Count == 2 && files.Items.All(a => a.Pinned), "files pinned with the briefing");

            int cap = f.Server.Context.Settings.Storage.MaxPinnedArtifacts;
            try
            {
                f.Server.Context.Settings.Storage.MaxPinnedArtifacts = 1;
                Run second = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
                Briefing b2 = await RunBriefingForAsync(f, second, token).ConfigureAwait(false);
                TestResponse over = (await f.User.Client.PutAsync("/v1.0/api/briefings/" + b2.Id + "/pin", new { pinned = true }, token).ConfigureAwait(false)).Expect(429, "pin cap");
                Assert.True(over.Body.Contains("QuotaExceeded", StringComparison.Ordinal), "quota error");
            }
            finally
            {
                f.Server.Context.Settings.Storage.MaxPinnedArtifacts = cap;
            }

            Artifact md = (await f.Server.Context.Database.Artifacts.ReadAsync(b.TenantId, b.MarkdownArtifactId ?? (await f.Server.Context.Database.Briefings.ReadAsync(b.TenantId, b.Id, token).ConfigureAwait(false))!.MarkdownArtifactId!, token).ConfigureAwait(false))!;
            await f.Server.Context.Artifacts.DeleteContentAsync(md, token).ConfigureAwait(false);
            (await f.User.Client.GetAsync("/v1.0/api/briefings/" + b.Id + "/download?format=md", token).ConfigureAwait(false)).Expect(410, "expired file");
            Assert.True((await f.User.Client.GetAsync("/v1.0/api/briefings/" + b.Id, token).ConfigureAwait(false)).Json<BriefingDetailResponse>().FilesExpired, "detail reports expired files");
            (await f.User.Client.PostAsync("/v1.0/api/briefings/" + b.Id + "/regenerate", null, token).ConfigureAwait(false)).Expect(200, "regenerate");
            TestResponse restored = (await f.User.Client.GetAsync("/v1.0/api/briefings/" + b.Id + "/download?format=md", token).ConfigureAwait(false)).Expect(200, "restored");
            Assert.True(restored.Body.StartsWith("# ", StringComparison.Ordinal), "restored Markdown");

            (await f.User.Client.DeleteAsync("/v1.0/api/briefings/" + b.Id, token).ConfigureAwait(false)).Expect(204, "delete");
            (await f.User.Client.GetAsync("/v1.0/api/briefings/" + b.Id, token).ConfigureAwait(false)).Expect(404, "gone");
            Assert.Equal(0, (await f.User.Client.GetAsync("/v1.0/api/artifacts?briefingId=" + b.Id, token).ConfigureAwait(false)).Json<EnumerationResult<Artifact>>().Items.Count, "files deleted");
        }
    }
}
