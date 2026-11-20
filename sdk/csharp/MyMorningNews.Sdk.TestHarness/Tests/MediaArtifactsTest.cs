namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Audio episodes and stored files: list, read, ranged download into memory and into a stream, download URL, and 404s.
    /// </summary>
    public class MediaArtifactsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Media and artifacts list, get, ranged download";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public MediaArtifactsTest()
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
            EnumerationResult<MediaAsset> media = await StepAsync(context, result, "GET media", () => c.Media.ListAsync(new MediaListOptions { PageSize = 5 }, token)).ConfigureAwait(false);
            if (media.Items.Count > 0)
            {
                MediaDetailResponse m = await StepAsync(context, result, "GET media item", () => c.Media.GetAsync(media.Items[0].Id, token)).ConfigureAwait(false);
                Helpers.Equal(media.Items[0].Id, m.Media.Id, "media id");
            }

            await StepAsync(context, result, "GET unknown media (404)", () => Helpers.ExpectErrorAsync(() => c.Media.GetAsync("med_missing", token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "GET unknown artifact (404)", () => Helpers.ExpectErrorAsync(() => c.Artifacts.GetAsync("art_missing", token), 404)).ConfigureAwait(false);

            EnumerationResult<Artifact> files = await StepAsync(context, result, "GET artifacts", () => c.Artifacts.ListAsync(new ArtifactListOptions { PageSize = 20 }, token)).ConfigureAwait(false);
            Artifact? file = files.Items.FirstOrDefault(a => a.DeletedUtc == null && a.Bytes >= 16);
            if (file == null) return;
            Artifact meta = await StepAsync(context, result, "GET artifact", () => c.Artifacts.GetAsync(file.Id, token)).ConfigureAwait(false);
            Helpers.Equal(file.Bytes, meta.Bytes, "artifact size");
            DownloadResult whole = await StepAsync(context, result, "GET artifact content", () => c.Artifacts.DownloadAsync(file.Id, null, true, token)).ConfigureAwait(false);
            Helpers.Equal(file.Bytes, whole.Length, "whole file length");
            Helpers.NotEmpty(whole.FileName, "file name from Content-Disposition");
            DownloadResult part = await StepAsync(context, result, "GET artifact content with Range", () => c.Artifacts.DownloadAsync(file.Id, new ByteRange(4, 11), false, token)).ConfigureAwait(false);
            Helpers.Equal(206, part.StatusCode, "partial status");
            Helpers.Equal(8L, part.Length, "partial length");
            Helpers.Equal(file.Bytes, part.TotalLength ?? -1, "total from Content-Range");
            Helpers.True(part.Data.SequenceEqual(whole.Data.Skip(4).Take(8)), "range bytes match");
            using (MemoryStream ms = new MemoryStream())
            {
                DownloadResult streamed = await StepAsync(context, result, "GET artifact content into a stream", () => c.Artifacts.DownloadToAsync(file.Id, ms, null, token)).ConfigureAwait(false);
                Helpers.Equal(file.Bytes, streamed.Length, "streamed length");
                Helpers.True(ms.ToArray().SequenceEqual(whole.Data), "streamed bytes match");
            }

            DownloadUrlResponse url = await StepAsync(context, result, "GET artifact download-url", () => c.Artifacts.GetDownloadUrlAsync(file.Id, token)).ConfigureAwait(false);
            Helpers.True(url.ContentPath.EndsWith("/content", System.StringComparison.Ordinal), "content path");
        }
    }
}
