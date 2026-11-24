namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Briefings: list, latest, read, Markdown and HTML downloads (with a byte range), and typed errors. Uses existing briefings
    /// when the server has any; composing new ones needs --with-run.
    /// </summary>
    public class BriefingsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Briefings list, get, download Markdown and HTML";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BriefingsTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            await StepAsync(context, result, "GET unknown briefing (404)", () => Helpers.ExpectErrorAsync(() => c.Briefings.GetAsync("brf_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "GET unknown briefing download (404)", () => Helpers.ExpectErrorAsync(() => c.Briefings.DownloadAsync("brf_missing", "md", null, token), 404)).ConfigureAwait(false);
            EnumerationResult<Briefing> page = await StepAsync(context, result, "GET briefings (ready)",
                () => c.Briefings.ListAsync(new BriefingListOptions { Status = BriefingStatusEnum.Ready, PageSize = 10 }, token)).ConfigureAwait(false);
            Helpers.True(page.Objects.All(b => b.Markdown == null), "lists leave Markdown out");
            await StepAsync(context, result, "GetLatestAsync", () => c.Briefings.GetLatestAsync(null, token)).ConfigureAwait(false);

            Briefing? ready = page.Objects.FirstOrDefault(b => b.MarkdownArtifactId != null);
            if (ready == null) return;
            BriefingDetailResponse detail = await StepAsync(context, result, "GET briefing", () => c.Briefings.GetAsync(ready.Id, token)).ConfigureAwait(false);
            Helpers.Equal(ready.Id, detail.Briefing.Id, "briefing id");
            if (detail.FilesExpired) return;

            string md = await StepAsync(context, result, "GET briefing download (md)", () => c.Briefings.GetMarkdownAsync(ready.Id, token)).ConfigureAwait(false);
            Helpers.NotEmpty(md, "Markdown");
            string html = await StepAsync(context, result, "GET briefing download (html)", () => c.Briefings.GetHtmlAsync(ready.Id, token)).ConfigureAwait(false);
            Helpers.True(html.Contains('<'), "HTML");
            DownloadResult part = await StepAsync(context, result, "GET briefing download with Range", () => c.Briefings.DownloadAsync(ready.Id, "md", new ByteRange(0, 9), token)).ConfigureAwait(false);
            Helpers.Equal(206, part.StatusCode, "partial status");
            Helpers.Equal(10L, part.Length, "partial length");
            await StepAsync(context, result, "GET briefing download format=pdf (400)", () => Helpers.ExpectErrorAsync(() => c.Briefings.DownloadAsync(ready.Id, "pdf", null, token), 400)).ConfigureAwait(false);
        }
    }
}
