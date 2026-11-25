namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Database.Postgresql;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Real PostgreSQL/disk and local origin tests of shared crawl caching and forced retrieval.</summary>
    public static class CrawlCacheSuite
    {
        private const string S = "CrawlCache";
        /// <summary>Cache regression suite.</summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Temporary crawl storage and retrieval", new[]
            {
                Cases.Case(S,"Settings","Nested secrets, validation, environment overrides and restart metadata are correct",SettingsAsync),
                Cases.Case(S,"S3","Blobject S3 streams, caches, refreshes and cleans a private prefix",S3Async),
                Cases.Case(S,"IdentityPolicy","URL identity and HTTP freshness preserve response semantics",IdentityAsync,"fast"),
                Cases.Case(S,"Pipeline","Stored source text cannot bypass cache freshness or policy; cleanup preserves evidence text",PipelineAsync),
                Cases.Case(S,"ReuseRefresh","Disk cache survives restart, forces retrieval, expires, and preserves source time",ReuseAsync),
                Cases.Case(S,"Concurrent","Two replicas coordinate fills; forced requests still retrieve; stale writers are fenced",ConcurrentAsync),
                Cases.Case(S,"Admission","Private, cookie, varying, sensitive, redirected and oversized responses bypass caching",AdmissionAsync),
                Cases.Case(S,"FailureCleanup","Corruption, timeout, partial writes and delete failures recover without losing live results",FailureAsync),
                Cases.Case(S,"Capacity","Reservations and metadata stay bounded, and abandoned leases are recovered",CapacityAsync),
                Cases.Case(S,"Diagnostic","HTTP diagnostic validates forceRefresh and preserves authorization",DiagnosticAsync)
            });
        }

        private static async Task SettingsAsync(CancellationToken token)
        {
            string file=Path.Combine(Path.GetTempPath(), "mmn-cache-settings-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                ServerSettings initial=new ServerSettings();
                initial.CrawlCache.Storage.S3AccessKey="file-access"; initial.CrawlCache.Storage.S3SecretKey="file-secret";
                await File.WriteAllTextAsync(file,MyMorningNews.Core.Serialization.Serializer.Serialize(initial),token).ConfigureAwait(false);
                MyMorningNews.Core.Services.SettingsService service=new MyMorningNews.Core.Services.SettingsService(file,n=>n=="MYMORNINGNEWS_CRAWL_CACHE_S3_SECRET_KEY"?"env-secret":null);
                MyMorningNews.Core.Responses.SettingsResponse described=service.Describe();
                Assert.Equal(MyMorningNews.Core.Services.SettingsService.SecretMask,described.Settings.CrawlCache.Storage.S3SecretKey,"secret masked");
                Assert.True(described.Fields.Any(f=>f.Path=="crawlCache.storage.s3SecretKey" && f.Secret && f.RestartRequired && f.OverriddenByEnvironment),"nested field metadata");
                described.Settings.CrawlCache.FreshnessSeconds=120;
                Assert.True((await service.UpdateAsync(described.Settings,token).ConfigureAwait(false)).Success,"live TTL updated");
                Assert.Equal(120,service.Current.CrawlCache.FreshnessSeconds,"live reference updated");
                Assert.Equal("env-secret",service.Current.CrawlCache.Storage.S3SecretKey,"environment wins");
                Assert.True(!File.ReadAllText(file).Contains("env-secret",StringComparison.Ordinal),"environment secret never persisted");
                described=service.Describe(); described.Settings.CrawlCache.Enabled=true; described.Settings.CrawlCache.Storage.Provider="S3";
                Assert.True(!(await service.UpdateAsync(described.Settings,token).ConfigureAwait(false)).Success,"missing S3 bucket rejected");
                described=service.Describe(); described.Settings.CrawlCache.MaxTotalBytes=65536;
                Assert.True(!(await service.UpdateAsync(described.Settings,token).ConfigureAwait(false)).Success,"cross-field capacity rejected");
            }
            finally { File.Delete(file); }
        }

        private static async Task S3Async(CancellationToken token)
        {
            MyMorningNewsServer server=await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            using (StubS3Server stub=new StubS3Server())
            {
                ServerSettings s=Settings();
                string? endpoint=Environment.GetEnvironmentVariable("MYMORNINGNEWS_TEST_CRAWL_S3_ENDPOINT");
                if (endpoint!=null && new Uri(endpoint).Host!="127.0.0.1") throw new InvalidOperationException("Crawl S3 integration fixtures must be local.");
                s.CrawlCache.Storage.Provider="S3";
                s.CrawlCache.Storage.S3Endpoint=endpoint??stub.Endpoint;
                s.CrawlCache.Storage.S3Bucket="mmn-crawl-test";
                s.CrawlCache.Storage.S3Region="us-east-1";
                s.CrawlCache.Storage.S3AccessKey="mmn-crawl-test";
                s.CrawlCache.Storage.S3SecretKey="mmn-crawl-test-password";
                if (endpoint!=null)
                {
                    using (Amazon.S3.AmazonS3Client client=new Amazon.S3.AmazonS3Client(s.CrawlCache.Storage.S3AccessKey,s.CrawlCache.Storage.S3SecretKey,new Amazon.S3.AmazonS3Config { ServiceURL=endpoint,ForcePathStyle=true }))
                    {
                        try { await client.PutBucketAsync(new Amazon.S3.Model.PutBucketRequest { BucketName=s.CrawlCache.Storage.S3Bucket },token).ConfigureAwait(false); }
                        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode=="BucketAlreadyOwnedByYou") { }
                    }
                }
                string url=Url();
                using (FetchService fetch=Fetch(server,s))
                {
                    FetchResult live=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                    Assert.True(live.CacheUpdated,"S3 upload publishes: "+live.Error);
                    FetchResult hit=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                    Assert.True(hit.CacheStatus=="Hit" && hit.Text==live.Text,"S3 streamed read validates checksum");
                    Assert.True((await fetch.FetchAsync(url,null,token,true).ConfigureAwait(false)).CacheUpdated,"S3 forced replacement");
                    await PurgeAsync(server,s,fetch.Cache!,token).ConfigureAwait(false);
                    Assert.True((await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false)).All(n=>n==0),"S3 cleanup removes accounting");
                }
            }
        }

        private static ServerSettings Settings()
        {
            ServerSettings s = new ServerSettings();
            s.Search.PerDomainDelayMs = 0;
            s.CrawlCache.Enabled = true;
            s.CrawlCache.StorageNamespace = IdGenerator.Generate(IdPrefixes.CrawlCache);
            s.CrawlCache.Storage.Directory = Path.Combine(Path.GetTempPath(), "mmn-crawl-tests", s.CrawlCache.StorageNamespace);
            return s;
        }
        private static string Url(string suffix = "article") { return StubModelServer.Shared.BaseUrl + "/crawl-cache/" + IdGenerator.Generate(IdPrefixes.CrawlCache) + "/" + suffix; }
        private static int Count(string url) { return StubModelServer.Shared.CrawlRequests.TryGetValue(new Uri(url).AbsolutePath, out int n) ? n : 0; }
        private static FetchService Fetch(MyMorningNewsServer server, ServerSettings s, ICrawlContentStore? store = null) { return new FetchService(s, server.Context.Logging, server.Context.Database.CrawlCache, store); }
        private static Task<int> Sql(MyMorningNewsServer server, string sql, string ns, CancellationToken token)
        {
            return ((PostgresqlDatabaseDriver)server.Context.Database).ExecuteNonQueryAsync(sql, new object?[] { ns }, token);
        }
        private static async Task PurgeAsync(MyMorningNewsServer server, ServerSettings s, CrawlCacheService cache, CancellationToken token)
        {
            await Sql(server, "UPDATE crawlcachegenerations SET expiresutc=NOW()-INTERVAL '1 day',retryutc=NOW()-INTERVAL '1 day' WHERE storagenamespace=$1", s.CrawlCache.StorageNamespace, token).ConfigureAwait(false);
            await cache.CleanupAsync(token).ConfigureAwait(false);
        }

        private static Task IdentityAsync(CancellationToken token)
        {
            Assert.Equal("https://example.com/a/?b=2&a=1&a=3",CrawlCacheKey.Normalize("HTTPS://EXAMPLE.COM:443/a/?b=2&a=1&a=3#frag"),"strict identity");
            foreach (string other in new[] { "http://example.com/a/", "https://www.example.com/a/", "https://example.com/a", "https://example.com/A/" })
                Assert.True(CrawlCacheKey.Normalize(other)!=CrawlCacheKey.Normalize("https://example.com/a/"),"distinct representation");
            foreach (string privateUrl in new[] { "https://u:p@example.com/", "https://example.com/?token=x", "https://example.com/?X-Amz-Signature=x", "file:///tmp/test" })
                Assert.True(CrawlCacheKey.Normalize(privateUrl)==null,"private URL bypass");
            DateTime now = DateTime.UtcNow;
            NameValueCollection headers = new NameValueCollection { { "Cache-Control", "max-age=60, s-maxage=30" }, { "Age", "10" } };
            DateTime? expiry = CrawlCachePolicy.Expiry(headers,now,now,86400,false);
            Assert.True(expiry.HasValue && Math.Abs((expiry.Value-now).TotalSeconds-20)<0.01,"shared age honored");
            headers["Age"]="40";
            Assert.True(CrawlCachePolicy.Expiry(headers,now,now,86400,false)==null,"already stale");
            headers.Clear(); headers["Cache-Control"]="private";
            Assert.True(CrawlCachePolicy.Expiry(headers,now,now,86400,false)==null,"private");
            headers.Clear(); headers["Vary"]="Accept-Encoding";
            Assert.True(CrawlCachePolicy.Expiry(headers,now,now,86400,false).HasValue,"stable encoding variant");
            Assert.True(CrawlCachePolicy.Expiry(headers,now,now,86400,true)==null,"redirect headers unavailable, bypass");
            headers.Clear(); headers["Expires"]=now.AddMinutes(-1).ToString("R");
            Assert.True(CrawlCachePolicy.Expiry(headers,now,now,86400,false)==null,"expired header");
            return Task.CompletedTask;
        }

        private static async Task PipelineAsync(CancellationToken token)
        {
            MyMorningNewsServer server=await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            MyMorningNews.Core.Models.Tenant tenant=await TestFixtures.CreateTenantAsync(await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false),token).ConfigureAwait(false);
            MyMorningNews.Core.Models.Run run=await server.Context.Database.Runs.CreateAsync(new MyMorningNews.Core.Models.Run { TenantId=tenant.Id, TopicId=IdGenerator.GenerateTopicId(), UserId="test" },token).ConfigureAwait(false);
            MyMorningNews.Core.Models.SearchQuery query=await server.Context.Database.SearchQueries.CreateAsync(new MyMorningNews.Core.Models.SearchQuery { TenantId=tenant.Id, RunId=run.Id, TopicId=run.TopicId, QueryText="cache" },token).ConfigureAwait(false);
            string url=Url(); string hash=UrlCanonicalizer.Hash(url)!;
            MyMorningNews.Core.Models.SearchResult selected=await server.Context.Database.SearchResults.CreateAsync(new MyMorningNews.Core.Models.SearchResult { TenantId=tenant.Id,RunId=run.Id,TopicId=run.TopicId,SearchQueryId=query.Id,Url=url,CanonicalUrlHash=hash },token).ConfigureAwait(false);
            MyMorningNews.Core.Models.SourceItem legacy=await server.Context.Database.SourceItems.CreateAsync(new MyMorningNews.Core.Models.SourceItem { TenantId=tenant.Id,CanonicalUrl=url,CanonicalUrlHash=hash,FetchedUtc=DateTime.UtcNow,ContentText="legacy source" },token).ConfigureAwait(false);
            ServerSettings s=Settings();
            using (FetchService fetch=Fetch(server,s))
            {
                MyMorningNews.Core.Pipeline.PipelineServices services=new MyMorningNews.Core.Pipeline.PipelineServices(server.Context.Database,server.Context.Models,server.Context.Prompts,server.Context.Search,fetch,s,server.Context.Logging);
                MyMorningNews.Core.Pipeline.RunContext ctx=new MyMorningNews.Core.Pipeline.RunContext { Run=run, Selected=new System.Collections.Generic.List<MyMorningNews.Core.Models.SearchResult> { selected } };
                MyMorningNews.Core.Pipeline.Stages.FetchStage stage=new MyMorningNews.Core.Pipeline.Stages.FetchStage();
                await stage.ExecuteAsync(ctx,services,token).ConfigureAwait(false);
                Assert.True(Count(url)==1 && ctx.Sources.Single().ContentText!="legacy source","legacy seven-day shortcut removed");
                DateTime fetched=ctx.Sources.Single().FetchedUtc;
                ctx.Sources.Clear();
                await stage.ExecuteAsync(ctx,services,token).ConfigureAwait(false);
                Assert.True(Count(url)==1 && Math.Abs((ctx.Sources.Single().FetchedUtc-fetched).TotalMilliseconds)<1,"pipeline hit retains origin timestamp");
                ctx.Sources.Clear();
                ctx.Constraints.Add(new MyMorningNews.Core.Models.TopicConstraint { Kind=ConstraintKindEnum.BlockedDomain,Value="127.0.0.1" });
                await stage.ExecuteAsync(ctx,services,token).ConfigureAwait(false);
                Assert.True(!ctx.Sources.Any() && Count(url)==1,"stored source and cache cannot bypass new domain rule");
                await PurgeAsync(server,s,fetch.Cache!,token).ConfigureAwait(false);
                MyMorningNews.Core.Models.SourceItem? preserved=await server.Context.Database.SourceItems.ReadAsync(tenant.Id,legacy.Id,token).ConfigureAwait(false);
                Assert.True(preserved?.ContentText!=null && preserved.ContentText.Contains("Origin version",StringComparison.Ordinal),"cleanup preserves source evidence text");
            }
        }

        private static async Task ReuseAsync(CancellationToken token)
        {
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ServerSettings s = Settings(); string url=Url(); FetchResult first;
            using (FetchService fetch=Fetch(server,s))
            {
                first=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                Assert.True(first.Disposition==FetchDispositionEnum.Fetched && first.CacheUpdated,"first publishes: "+first.Error);
                FetchResult hit=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                Assert.True(hit.CacheStatus=="Hit" && hit.Text==first.Text && Count(url)==1 && hit.NetworkBytes==0,"hit avoids origin");
                Assert.True(Math.Abs((hit.FetchedUtc-first.FetchedUtc).TotalMilliseconds)<1,"original timestamp");
                FetchResult forced=await fetch.FetchAsync(url,null,token,true).ConfigureAwait(false);
                Assert.True(forced.CacheStatus=="Forced" && forced.CacheUpdated && forced.Text!=first.Text && Count(url)==2,"force replaces");
                StubModelServer.Shared.CrawlFailures[new Uri(url).AbsolutePath]=true;
                FetchResult failure=await fetch.FetchAsync(url,null,token,true).ConfigureAwait(false);
                Assert.True(failure.Disposition==FetchDispositionEnum.HttpError && failure.CacheStatus=="Forced" && !failure.CacheUpdated,"no fallback on force failure");
                hit=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                Assert.True(hit.CacheStatus=="Hit" && hit.Text==forced.Text,"transient failure preserves eligible prior generation");
                StubModelServer.Shared.CrawlFailures.TryRemove(new Uri(url).AbsolutePath,out bool ignored);
            }
            using (FetchService restarted=Fetch(server,s))
            {
                Assert.Equal("Hit",(await restarted.FetchAsync(url,null,token).ConfigureAwait(false)).CacheStatus,"restart persistent");
                await Sql(server,"UPDATE crawlcachegenerations SET expiresutc=NOW()-INTERVAL '1 second' WHERE storagenamespace=$1",s.CrawlCache.StorageNamespace,token).ConfigureAwait(false);
                Assert.True((await restarted.FetchAsync(url,null,token).ConfigureAwait(false)).CacheUpdated,"expired refetches");
                Assert.Equal(FetchDispositionEnum.Blocked,(await restarted.FetchAsync(url,new[]{"127.0.0.1"},token).ConfigureAwait(false)).Disposition,"current domain policy precedes hit");
                bool original=server.Context.Settings.Hosting.RestrictPrivateNetworks;
                try
                {
                    server.Context.Settings.Hosting.RestrictPrivateNetworks=true;
                    Assert.Equal(FetchDispositionEnum.Blocked,(await restarted.FetchAsync(url,null,token).ConfigureAwait(false)).Disposition,"network policy precedes hit");
                }
                finally { server.Context.Settings.Hosting.RestrictPrivateNetworks=original; }
                s.CrawlCache.Enabled=false;
                Assert.Equal("Bypass",(await restarted.FetchAsync(url,null,token).ConfigureAwait(false)).CacheStatus,"disabled goes live");
                await PurgeAsync(server,s,restarted.Cache!,token).ConfigureAwait(false);
                Assert.Equal(0L,(await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false))[1],"cleanup while disabled");
            }
        }

        private static async Task ConcurrentAsync(CancellationToken token)
        {
            MyMorningNewsServer server=await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ServerSettings s=Settings(); string url=Url("slow");
            using (FetchService one=Fetch(server,s))
            using (FetchService two=Fetch(server,s))
            {
                FetchResult[] results=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>(i%2==0?one:two).FetchAsync(url,null,token))).ConfigureAwait(false);
                Assert.True(Count(url)==1 && results.All(r=>r.Disposition==FetchDispositionEnum.Fetched),"one fill across replicas");
                await Task.WhenAll(one.FetchAsync(url,null,token,true),two.FetchAsync(url,null,token,true)).ConfigureAwait(false);
                Assert.Equal(3,Count(url),"each forced caller retrieves anew");
                s.CrawlCache.WaitForFillMs=0;
                string profile=CrawlCacheKey.Profile(s.Search); string normalized=CrawlCacheKey.Normalize(url)!; string key=CrawlCacheKey.Hash(normalized+"\n"+profile);
                CrawlCacheLease held=(await server.Context.Database.CrawlCache.AcquireAsync(s.CrawlCache.StorageNamespace,key,normalized,profile,s.CrawlCache,token).ConfigureAwait(false))!;
                FetchResult forced=await two.FetchAsync(url,null,token,true).ConfigureAwait(false);
                Assert.True(forced.CacheStatus=="Forced" && !forced.CacheUpdated && Count(url)==4,"bounded contention fetches without unsafe publication");
                await Sql(server,"UPDATE crawlcacheentries SET leaseuntil=NOW()-INTERVAL '1 second' WHERE storagenamespace=$1",s.CrawlCache.StorageNamespace,token).ConfigureAwait(false);
                CrawlCacheLease successor=(await server.Context.Database.CrawlCache.AcquireAsync(s.CrawlCache.StorageNamespace,key,normalized,profile,s.CrawlCache,token).ConfigureAwait(false))!;
                Assert.True(successor.Token!=held.Token && !await server.Context.Database.CrawlCache.RenewAsync(held,180,token).ConfigureAwait(false),"expired owner fenced");
                Assert.True(!await server.Context.Database.CrawlCache.PublishAsync(held,"missing",token).ConfigureAwait(false),"late publish denied");
                await server.Context.Database.CrawlCache.ReleaseAsync(held,token).ConfigureAwait(false);
                Assert.True(await server.Context.Database.CrawlCache.RenewAsync(successor,180,token).ConfigureAwait(false),"old release cannot remove successor");
                await server.Context.Database.CrawlCache.ReleaseAsync(successor,token).ConfigureAwait(false);
                using (CancellationTokenSource canceled=new CancellationTokenSource())
                {
                    canceled.Cancel(); bool threw=false;
                    try { await two.FetchAsync(url,null,canceled.Token,true).ConfigureAwait(false); }
                    catch (OperationCanceledException) { threw=true; }
                    Assert.True(threw,"cancellation propagated");
                }
                s.CrawlCache.WaitForFillMs=5000;
                string cancelUrl=Url("slow");
                Task<FetchResult> owner=one.FetchAsync(cancelUrl,null,token);
                using (CancellationTokenSource waiting=CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    DateTime deadline=DateTime.UtcNow.AddSeconds(5);
                    while (Count(cancelUrl)==0 && DateTime.UtcNow<deadline) await Task.Delay(10,token).ConfigureAwait(false);
                    waiting.CancelAfter(30); bool canceled=false;
                    try { await two.FetchAsync(cancelUrl,null,waiting.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { canceled=true; }
                    Assert.True(canceled,"a waiting caller can cancel");
                }
                Assert.True((await owner.ConfigureAwait(false)).CacheUpdated && Count(cancelUrl)==1,"waiter cancellation does not cancel owner");
                await PurgeAsync(server,s,one.Cache!,token).ConfigureAwait(false);
            }
        }

        private static async Task AdmissionAsync(CancellationToken token)
        {
            MyMorningNewsServer server=await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ServerSettings s=Settings(); s.CrawlCache.MaxEntryBytes=65536;
            using (FetchService fetch=Fetch(server,s))
            {
                foreach (string suffix in new[]{"private","no-store","no-cache","cookie","vary","redirect","large","article?token=secret"})
                {
                    string url=Url(suffix);
                    FetchResult first=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                    FetchResult second=await fetch.FetchAsync(url,null,token).ConfigureAwait(false);
                    Assert.True(!first.CacheUpdated && second.CacheStatus!="Hit" && Count(url)>=2,"bypass "+suffix);
                }
                Assert.Equal(0L,(await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false))[1],"no ineligible objects");
                string robotsUrl=StubModelServer.Shared.BaseUrl+"/private/crawl-cache/"+IdGenerator.Generate(IdPrefixes.CrawlCache);
                s.Search.RespectRobots=false;
                Assert.True((await fetch.FetchAsync(robotsUrl,null,token).ConfigureAwait(false)).CacheUpdated,"seed before robots enforcement");
                s.Search.RespectRobots=true;
                Assert.Equal(FetchDispositionEnum.RobotsDenied,(await fetch.FetchAsync(robotsUrl,null,token,true).ConfigureAwait(false)).Disposition,"forced request respects robots");
                Assert.Equal(FetchDispositionEnum.RobotsDenied,(await fetch.FetchAsync(robotsUrl,null,token).ConfigureAwait(false)).Disposition,"hit respects current robots");
                await PurgeAsync(server,s,fetch.Cache!,token).ConfigureAwait(false);
            }
        }

        private static async Task FailureAsync(CancellationToken token)
        {
            MyMorningNewsServer server=await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ServerSettings s=Settings(); s.CrawlCache.OperationTimeoutMs=100;
            using (PostgresqlDatabaseDriver unavailable=new PostgresqlDatabaseDriver(new MyMorningNews.Core.Database.DatabaseSettings()))
            using (FetchService liveOnly=new FetchService(s,server.Context.Logging,unavailable.CrawlCache))
            {
                FetchResult live=await liveOnly.FetchAsync(Url(),null,token).ConfigureAwait(false);
                Assert.True(live.Disposition==FetchDispositionEnum.Fetched && live.CacheStatus=="Error" && !live.CacheUpdated,"unavailable cache database preserves origin result");
            }
            FaultingCrawlStore store=new FaultingCrawlStore(new BlobjectCrawlContentStore(s.CrawlCache));
            using (FetchService fetch=Fetch(server,s,store))
            {
                string url=Url();
                Assert.True((await fetch.FetchAsync(url,null,token).ConfigureAwait(false)).CacheUpdated,"initial publish");
                string text=Directory.GetFiles(s.CrawlCache.Storage.Directory,"content.md",SearchOption.AllDirectories).Single();
                await File.WriteAllTextAsync(text,"corrupt",token).ConfigureAwait(false);
                Assert.True((await fetch.FetchAsync(url,null,token).ConfigureAwait(false)).CacheUpdated && Count(url)==2,"corrupt object repaired");
                string raw=Directory.GetFiles(s.CrawlCache.Storage.Directory,"raw",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal).Last();
                File.Delete(raw);
                await Sql(server,"UPDATE crawlcachegenerations SET checkedutc=NOW()-INTERVAL '1 hour' WHERE storagenamespace=$1",s.CrawlCache.StorageNamespace,token).ConfigureAwait(false);
                await fetch.Cache!.CleanupAsync(token).ConfigureAwait(false);
                Assert.True((await fetch.FetchAsync(url,null,token).ConfigureAwait(false)).CacheUpdated,"missing raw invalidates on integrity pass");
                store.SlowReads=true;
                Assert.Equal(FetchDispositionEnum.Fetched,(await fetch.FetchAsync(url,null,token).ConfigureAwait(false)).Disposition,"timeout falls back");
                store.SlowReads=false; store.FailWrites=true;
                FetchResult result=await fetch.FetchAsync(url,null,token,true).ConfigureAwait(false);
                Assert.True(result.Disposition==FetchDispositionEnum.Fetched && !result.CacheUpdated,"partial write does not fail live fetch");
                store.FailWrites=false; store.FailDeletes=true;
                await PurgeAsync(server,s,fetch.Cache!,token).ConfigureAwait(false);
                Assert.True((await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false))[1]>0,"failed deletes retain accounting");
                store.FailDeletes=false;
                await PurgeAsync(server,s,fetch.Cache!,token).ConfigureAwait(false);
                Assert.True((await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false)).All(n=>n==0),"retry removes metadata and bytes");
                Assert.Equal(0,Directory.GetFiles(s.CrawlCache.Storage.Directory,"*",SearchOption.AllDirectories).Length,"no orphan content");
            }
        }

        private static async Task CapacityAsync(CancellationToken token)
        {
            MyMorningNewsServer server=await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            ServerSettings s=Settings(); s.CrawlCache.MaxEntries=2; s.CrawlCache.WaitForFillMs=0;
            using (FetchService fetch=Fetch(server,s))
            {
                await Task.WhenAll(Enumerable.Range(0,6).Select(_=>fetch.FetchAsync(Url(),null,token))).ConfigureAwait(false);
                long[] usage=await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false);
                Assert.True(usage[1]<=2 && usage[2]<=2,"atomic admission bounded");
                await PurgeAsync(server,s,fetch.Cache!,token).ConfigureAwait(false);
                CrawlCacheLease? abandoned=await server.Context.Database.CrawlCache.AcquireAsync(s.CrawlCache.StorageNamespace,"key","https://example.test/","p",s.CrawlCache,token).ConfigureAwait(false);
                Assert.True(abandoned!=null,"lease created");
                Assert.True(!await server.Context.Database.CrawlCache.BindAsync(s.CrawlCache.StorageNamespace,"different-storage-location",token).ConfigureAwait(false),"namespace cannot silently switch stores");
                await Sql(server,"UPDATE crawlcacheentries SET leaseuntil=NOW()-INTERVAL '1 second' WHERE storagenamespace=$1",s.CrawlCache.StorageNamespace,token).ConfigureAwait(false);
                await fetch.Cache!.CleanupAsync(token).ConfigureAwait(false);
                Assert.True((await server.Context.Database.CrawlCache.UsageAsync(s.CrawlCache.StorageNamespace,token).ConfigureAwait(false)).All(n=>n==0),"abandoned empty lease reclaimed");
            }
        }

        private static async Task DiagnosticAsync(CancellationToken token)
        {
            TestApiClient admin=await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            (await admin.PostAsync("/v1.0/api/fetch/test",new { url=Url(), forceRefresh="true" },token).ConfigureAwait(false)).Expect(400,"strict boolean");
            FetchResult forced=(await admin.PostAsync("/v1.0/api/fetch/test",new { url=Url(), forceRefresh=true },token).ConfigureAwait(false)).Expect(200,"forced diagnostic").Json<FetchResult>();
            Assert.Equal("Forced",forced.CacheStatus,"option propagated");
            MyMorningNews.Core.Models.Tenant tenant=await TestFixtures.CreateTenantAsync(admin,token).ConfigureAwait(false);
            TestUser member=await TestFixtures.CreateUserAsync(admin,tenant.Id,false,token).ConfigureAwait(false);
            (await member.Client.PostAsync("/v1.0/api/fetch/test",new { url=Url(), forceRefresh=true },token).ConfigureAwait(false)).Expect(403,"members cannot force through admin diagnostic");
        }
    }
}
