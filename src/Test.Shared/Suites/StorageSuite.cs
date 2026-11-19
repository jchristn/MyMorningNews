namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Storage;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Artifact storage (plan section 22.1): the local and S3 stores through the same checks, retention, pinning, and
    /// ranged HTTP downloads.
    /// </summary>
    public static class StorageSuite
    {
        private const string S = "Storage";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Artifact storage", new[]
            {
                Cases.Case(S, "LocalStore", "The local store writes, reads ranges, deletes, and refuses keys that escape its directory", LocalAsync, "fast"),
                Cases.Case(S, "S3Store", "The S3 store writes, reads ranges, presigns, and deletes against an S3-compatible server", S3Async),
                Cases.Case(S, "Retention", "Expired files are deleted from the store and marked; pinned files are kept", RetentionAsync),
                Cases.Case(S, "HttpRanges", "File downloads honor Range with 206 and Content-Range, and reject bad ranges with 416", RangesAsync)
            });
        }

        private static async Task CheckStoreAsync(IArtifactStore store, CancellationToken token)
        {
            string key = "ten_test/2026/11/art_" + Guid.NewGuid().ToString("N") + ".bin";
            byte[] data = Encoding.UTF8.GetBytes("0123456789abcdefghij");
            await store.PutAsync(key, data, "application/octet-stream", token).ConfigureAwait(false);
            using (ArtifactReadResult all = await store.OpenReadAsync(key, null, null, token).ConfigureAwait(false))
            {
                Assert.Equal(20L, all.TotalBytes, store.Provider + " total");
                Assert.Equal("0123456789abcdefghij", await ReadAsync(all.Stream, token).ConfigureAwait(false), store.Provider + " full read");
            }

            using (ArtifactReadResult part = await store.OpenReadAsync(key, 5, 4, token).ConfigureAwait(false))
            {
                Assert.True(part.Offset == 5 && part.Length == 4 && part.TotalBytes == 20, store.Provider + " range metadata (" + part.Offset + ", " + part.Length + ", " + part.TotalBytes + ")");
                Assert.Equal("5678", await ReadAsync(part.Stream, token).ConfigureAwait(false), store.Provider + " range read");
            }

            using (ArtifactReadResult tail = await store.OpenReadAsync(key, 15, null, token).ConfigureAwait(false))
                Assert.Equal("fghij", await ReadAsync(tail.Stream, token).ConfigureAwait(false), store.Provider + " open-ended range");
            await store.PutAsync(key, Encoding.UTF8.GetBytes("replaced"), "text/plain", token).ConfigureAwait(false);
            using (ArtifactReadResult r = await store.OpenReadAsync(key, null, null, token).ConfigureAwait(false))
                Assert.Equal("replaced", await ReadAsync(r.Stream, token).ConfigureAwait(false), store.Provider + " overwrite");
            await store.DeleteAsync(key, token).ConfigureAwait(false);
            await store.DeleteAsync(key, token).ConfigureAwait(false);
            bool missing = false;
            try
            {
                using (ArtifactReadResult gone = await store.OpenReadAsync(key, null, null, token).ConfigureAwait(false))
                {
                }
            }
            catch (FileNotFoundException)
            {
                missing = true;
            }

            Assert.True(missing, store.Provider + " missing after delete");
        }

        private static async Task LocalAsync(CancellationToken token)
        {
            string dir = Path.Combine(Path.GetTempPath(), "mmn-store-" + Guid.NewGuid().ToString("N"));
            using (LocalArtifactStore store = new LocalArtifactStore(dir))
            {
                await CheckStoreAsync(store, token).ConfigureAwait(false);
                bool refused = false;
                try
                {
                    await store.PutAsync("../outside.txt", new byte[] { 1 }, "text/plain", token).ConfigureAwait(false);
                }
                catch (ArgumentException)
                {
                    refused = true;
                }

                Assert.True(refused && !File.Exists(Path.Combine(Path.GetDirectoryName(dir)!, "outside.txt")), "traversal refused");
                Assert.True(await store.GetDownloadUrlAsync("a/b.bin", "b.bin", 5, token).ConfigureAwait(false) == null, "local store serves through the server");
            }

            Directory.Delete(dir, true);
        }

        private static async Task S3Async(CancellationToken token)
        {
            using (StubS3Server s3 = new StubS3Server())
            {
                StorageSettings settings = new StorageSettings { Provider = StorageProviderEnum.S3, S3Bucket = "mmn-test", S3Endpoint = s3.Endpoint, S3ForcePathStyle = true, S3AccessKey = "test", S3SecretKey = "test-secret", S3Prefix = "media" };
                using (S3ArtifactStore store = new S3ArtifactStore(settings))
                {
                    Assert.Equal("media/", store.Prefix, "prefix normalized");
                    await CheckStoreAsync(store, token).ConfigureAwait(false);
                    await store.PutAsync("ten_x/2026/11/a.md", Encoding.UTF8.GetBytes("# Hi"), "text/markdown", token).ConfigureAwait(false);
                    Assert.True(s3.Objects.ContainsKey("mmn-test/media/ten_x/2026/11/a.md"), "stored under bucket and prefix: " + String.Join(", ", s3.Objects.Keys));
                    Assert.Equal("# Hi", Encoding.UTF8.GetString(s3.Objects["mmn-test/media/ten_x/2026/11/a.md"]), "body not chunk-encoded");
                    string? url = await store.GetDownloadUrlAsync("ten_x/2026/11/a.md", "a.md", 15, token).ConfigureAwait(false);
                    Assert.True(url != null && url.StartsWith(s3.Endpoint + "/mmn-test/media/ten_x/2026/11/a.md", StringComparison.Ordinal) && url.Contains("X-Amz-Signature", StringComparison.Ordinal)
                        && url.Contains("response-content-disposition", StringComparison.OrdinalIgnoreCase), "presigned URL: " + url);
                    using (HttpClient http = new HttpClient())
                        Assert.Equal("# Hi", await http.GetStringAsync(url!, token).ConfigureAwait(false), "presigned URL downloads");
                }
            }
        }

        private static async Task<Artifact> SaveAsync(MyMorningNewsServer server, TestUser user, string text, CancellationToken token)
        {
            return await server.Context.Artifacts.SaveAsync(new ArtifactWrite
            {
                TenantId = user.User.TenantId, UserId = user.User.Id, Kind = ArtifactKindEnum.Export, FileName = "notes.txt", ContentType = "text/plain; charset=utf-8", Extension = "txt",
                Content = Encoding.UTF8.GetBytes(text)
            }, token).ConfigureAwait(false);
        }

        private static async Task RetentionAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Artifact old = await SaveAsync(server, u, "old", token).ConfigureAwait(false);
            Artifact kept = await SaveAsync(server, u, "kept", token).ConfigureAwait(false);
            Artifact fresh = await SaveAsync(server, u, "fresh", token).ConfigureAwait(false);
            Assert.True(old.ExpiresUtc!.Value > DateTime.UtcNow.AddDays(364) && old.Sha256.Length == 64 && old.Bytes == 3 && old.StorageKey.StartsWith(t.Id + "/", StringComparison.Ordinal), "365-day expiry, hash, size, tenant key");
            old.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            await server.Context.Database.Artifacts.UpdateAsync(old, token).ConfigureAwait(false);
            kept.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            kept.Pinned = true;
            await server.Context.Database.Artifacts.UpdateAsync(kept, token).ConfigureAwait(false);

            int expired = await server.Context.Artifacts.ExpireAsync(DateTime.UtcNow, 500, token).ConfigureAwait(false);
            Assert.True(expired >= 1, "expired at least one");
            Artifact oldNow = (await server.Context.Database.Artifacts.ReadAsync(t.Id, old.Id, token).ConfigureAwait(false))!;
            Assert.True(oldNow.DeletedUtc.HasValue, "expired file marked deleted");
            bool gone = false;
            try
            {
                await server.Context.Artifacts.ReadAllAsync(oldNow, token).ConfigureAwait(false);
            }
            catch (ArtifactExpiredException)
            {
                gone = true;
            }

            Assert.True(gone, "expired file reads as expired");
            Assert.Equal("kept", Encoding.UTF8.GetString(await server.Context.Artifacts.ReadAllAsync(kept, token).ConfigureAwait(false)), "pinned file kept");
            Assert.Equal("fresh", Encoding.UTF8.GetString(await server.Context.Artifacts.ReadAllAsync(fresh, token).ConfigureAwait(false)), "unexpired file kept");
            (await u.Client.GetAsync("/v1.0/api/artifacts/" + old.Id + "/content", token).ConfigureAwait(false)).Expect(410, "expired download is 410");
        }

        private static async Task RangesAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser u = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            TestUser other = await TestFixtures.CreateUserAsync(admin, t.Id, false, token).ConfigureAwait(false);
            Artifact a = await SaveAsync(server, u, "0123456789", token).ConfigureAwait(false);
            string path = "/v1.0/api/artifacts/" + a.Id + "/content";

            TestResponse full = (await u.Client.GetAsync(path + "?download=true", token).ConfigureAwait(false)).Expect(200, "full");
            Assert.True(full.Body == "0123456789" && full.Headers["Accept-Ranges"] == "bytes" && full.Headers["Content-Disposition"].StartsWith("attachment", StringComparison.Ordinal), "full body and headers");
            TestResponse part = (await u.Client.SendAsync(HttpMethod.Get, path, null, new Dictionary<string, string> { { "Range", "bytes=2-5" } }, token).ConfigureAwait(false)).Expect(206, "range");
            Assert.True(part.Body == "2345" && part.Headers["Content-Range"] == "bytes 2-5/10", "range body " + part.Body + " " + part.Headers.GetValueOrDefault("Content-Range"));
            TestResponse suffix = (await u.Client.SendAsync(HttpMethod.Get, path, null, new Dictionary<string, string> { { "Range", "bytes=-3" } }, token).ConfigureAwait(false)).Expect(206, "suffix");
            Assert.True(suffix.Body == "789" && suffix.Headers["Content-Range"] == "bytes 7-9/10", "suffix range");
            TestResponse open = (await u.Client.SendAsync(HttpMethod.Get, path, null, new Dictionary<string, string> { { "Range", "bytes=8-" } }, token).ConfigureAwait(false)).Expect(206, "open range");
            Assert.Equal("89", open.Body, "open range body");
            (await u.Client.SendAsync(HttpMethod.Get, path, null, new Dictionary<string, string> { { "Range", "bytes=20-30" } }, token).ConfigureAwait(false)).Expect(416, "unsatisfiable");
            DownloadUrlCheck(await u.Client.GetAsync("/v1.0/api/artifacts/" + a.Id + "/download-url", token).ConfigureAwait(false), a.Id);
            (await other.Client.GetAsync(path, token).ConfigureAwait(false)).Expect(404, "another member cannot read it");
        }

        private static void DownloadUrlCheck(TestResponse r, string id)
        {
            MyMorningNews.Core.Responses.DownloadUrlResponse d = r.Expect(200, "download url").Json<MyMorningNews.Core.Responses.DownloadUrlResponse>();
            Assert.True(d.Url == null && d.ContentPath == "/v1.0/api/artifacts/" + id + "/content", "local files download through the server");
        }

        private static async Task<string> ReadAsync(Stream s, CancellationToken token)
        {
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                return await r.ReadToEndAsync(token).ConfigureAwait(false);
        }
    }
}
