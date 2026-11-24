namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Server;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>Durable generated-audio accounting under concurrent workers and retries.</summary>
    public static class AudioQuotaSuite
    {
        /// <summary>Build quota tests.</summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor("AudioQuota", "Daily audio reservations", new[]
            {
                Cases.Case("AudioQuota", "Concurrency", "Reservations enforce the daily cap, exact settlement releases headroom, and replay/deletion never reset usage", ConcurrencyAsync),
                Cases.Case("AudioQuota", "QueueAndSamples", "Duplicate queue requests reserve once; transient voice samples share the exact daily ledger", QueueAndSamplesAsync)
            });
        }

        private static async Task QueueAndSamplesAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            Briefing briefing = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = tenant.Id, UserId = user.User.Id, Title = "Concurrent audio", Markdown = "Evidence", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            List<Task<MediaAsset?>> calls = new List<Task<MediaAsset?>>();
            for (int i = 0; i < 12; i++)
            {
                MediaAsset candidate = new MediaAsset { TenantId = tenant.Id, UserId = user.User.Id, BriefingId = briefing.Id, Title = "Same episode" };
                calls.Add(server.Context.Database.AudioReservations.TryQueueAsync(candidate, new AudioReservation
                { TenantId = tenant.Id, UserId = user.User.Id, OperationId = candidate.Id, PeriodStartUtc = DateTime.UtcNow.Date, ReservedSeconds = 30, ExpiresUtc = DateTime.UtcNow.AddHours(2) }, 30, false, token));
            }
            MediaAsset?[] results = await Task.WhenAll(calls).ConfigureAwait(false);
            Assert.True(results.All(r => r != null) && results.Select(r => r!.Id).Distinct().Count() == 1, "concurrent duplicate requests return the same queued episode even when it uses the entire allowance");
            AudioUsageSummary usage = await server.Context.Database.AudioReservations.UsageAsync(tenant.Id, user.User.Id, DateTime.UtcNow.Date, token).ConfigureAwait(false);
            Assert.Equal(30L, usage.ReservedSeconds, "one reservation");
            MediaAsset episode = results[0]!;
            Assert.True(await server.Context.Database.AudioReservations.TryCommitSampleAsync(tenant.Id, user.User.Id, episode.Id, 1000, 30, token).ConfigureAwait(false) == null, "sample settlement cannot bypass media publication");
            await server.Context.Database.AudioReservations.ReleaseAsync(tenant.Id, user.User.Id, episode.Id, token).ConfigureAwait(false);

            AudioQuotaService quotas = new AudioQuotaService(server.Context.Database, server.Context.Settings);
            AudioReservation sample = await quotas.ReserveSampleAsync(tenant.Id, user.User.Id, MyMorningNews.Core.Helpers.IdGenerator.GenerateMediaAssetId(), 10, token).ConfigureAwait(false);
            await quotas.CommitSampleAsync(sample, 1.501, token).ConfigureAwait(false);
            await quotas.CommitSampleAsync(sample, 1.501, token).ConfigureAwait(false);
            await server.Context.Database.AudioReservations.ReleaseAsync(tenant.Id, user.User.Id, sample.OperationId, token).ConfigureAwait(false);
            usage = await server.Context.Database.AudioReservations.UsageAsync(tenant.Id, user.User.Id, DateTime.UtcNow.Date, token).ConfigureAwait(false);
            Assert.True(usage.GeneratedMilliseconds == 1501 && usage.ReservedSeconds == 0, "transient sample charged exactly once with no stored media");
            Assert.True(await server.Context.Database.AudioReservations.TryCommitSampleAsync(tenant.Id, user.User.Id, sample.OperationId, 1000, 30, token).ConfigureAwait(false) == null, "committed sample duration is immutable");
            AudioReservation abandoned = await quotas.ReserveSampleAsync(tenant.Id, user.User.Id, MyMorningNews.Core.Helpers.IdGenerator.GenerateMediaAssetId(), 10, token).ConfigureAwait(false);
            await server.Context.Database.AudioReservations.RecoverAsync(DateTime.UtcNow.AddHours(3), token).ConfigureAwait(false);
            usage = await server.Context.Database.AudioReservations.UsageAsync(tenant.Id, user.User.Id, DateTime.UtcNow.Date, token).ConfigureAwait(false);
            Assert.True(usage.GeneratedMilliseconds == 1501 && usage.ReservedSeconds == 0 && abandoned.Id != sample.Id, "orphan preview recovery releases holds but preserves charges");
        }

        private static async Task ConcurrencyAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            MyMorningNewsServer server = await SharedTestServer.GetServerAsync(token).ConfigureAwait(false);
            Tenant tenant = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser user = await TestFixtures.CreateUserAsync(admin, tenant.Id, false, token).ConfigureAwait(false);
            Briefing briefing = await server.Context.Database.Briefings.CreateAsync(new Briefing { TenantId = tenant.Id, UserId = user.User.Id, Title = "Audio quota", Markdown = "Evidence", Status = BriefingStatusEnum.Ready }, token).ConfigureAwait(false);
            List<MediaAsset> assets = new List<MediaAsset>();
            for (int i = 0; i < 10; i++) assets.Add(await server.Context.Database.MediaAssets.CreateAsync(new MediaAsset { TenantId = tenant.Id, UserId = user.User.Id, BriefingId = briefing.Id, Title = "Generation " + i }, token).ConfigureAwait(false));
            DateTime day = DateTime.UtcNow.Date;
            AudioReservation?[] reservations = await Task.WhenAll(assets.Select(asset => server.Context.Database.AudioReservations.TryReserveAsync(new AudioReservation
            {
                TenantId = tenant.Id, UserId = user.User.Id, OperationId = asset.Id, PeriodStartUtc = day, ReservedSeconds = 6, ExpiresUtc = DateTime.UtcNow.AddHours(2)
            }, 30, token))).ConfigureAwait(false);
            Assert.Equal(5, reservations.Count(r => r != null), "only five six-second reservations fit");
            AudioReservation reserved = reservations.First(r => r != null)!;
            MediaAsset chosen = assets.First(a => a.Id == reserved.OperationId);
            chosen.DurationSeconds = 1.501;
            chosen.ContentType = "audio/wav";
            chosen.SegmentCount = 1;
            Assert.NotNull(await server.Context.Database.AudioReservations.TryCommitAsync(chosen, 1501, 30, token).ConfigureAwait(false), "commit exact duration");
            Assert.NotNull(await server.Context.Database.AudioReservations.TryCommitAsync(chosen, 1501, 30, token).ConfigureAwait(false), "commit retry idempotent");
            AudioUsageSummary usage = await server.Context.Database.AudioReservations.UsageAsync(tenant.Id, user.User.Id, day, token).ConfigureAwait(false);
            Assert.True(usage.GeneratedMilliseconds == 1501 && usage.ReservedSeconds == 24, "one exact charge and unused reservation released");
            await server.Context.Database.AudioReservations.ReleaseAsync(tenant.Id, user.User.Id, chosen.Id, token).ConfigureAwait(false);
            await server.Context.Database.MediaAssets.DeleteAsync(tenant.Id, chosen.Id, token).ConfigureAwait(false);
            usage = await server.Context.Database.AudioReservations.UsageAsync(tenant.Id, user.User.Id, day, token).ConfigureAwait(false);
            Assert.Equal(1501L, usage.GeneratedMilliseconds, "release and media deletion cannot refund generation");
            MediaAsset next = assets.First(a => reservations.All(r => r?.OperationId != a.Id));
            AudioReservation? denied = await server.Context.Database.AudioReservations.TryReserveAsync(new AudioReservation
            { TenantId = tenant.Id, UserId = user.User.Id, OperationId = next.Id, PeriodStartUtc = day, ReservedSeconds = 6, ExpiresUtc = DateTime.UtcNow.AddHours(2) }, 30, token).ConfigureAwait(false);
            Assert.True(denied == null, "4.499 seconds remaining cannot reserve six");
            AudioReservation nextDay = Assert.NotNull(await server.Context.Database.AudioReservations.TryReserveAsync(new AudioReservation
            { TenantId = tenant.Id, UserId = user.User.Id, OperationId = next.Id, PeriodStartUtc = day.AddDays(1), ReservedSeconds = 6, ExpiresUtc = day.AddDays(2) }, 30, token).ConfigureAwait(false), "UTC reset opens next day");
            AudioReservation old = reservations.First(r => r != null && r.Id != reserved.Id)!;
            MediaAsset overrun = assets.First(a => a.Id == old.OperationId);
            Assert.True(await server.Context.Database.AudioReservations.TryCommitAsync(overrun, 12000, 30, token).ConfigureAwait(false) == null, "oversized settlement cannot consume held capacity");
            await server.Context.Database.AudioReservations.ReleaseAsync(tenant.Id, user.User.Id, old.OperationId, token).ConfigureAwait(false);
            usage = await server.Context.Database.AudioReservations.UsageAsync(tenant.Id, user.User.Id, day, token).ConfigureAwait(false);
            Assert.True(usage.GeneratedMilliseconds == 1501 && usage.ReservedSeconds == 18 && nextDay.PeriodStartUtc > day, "failure releases only its own reservation");
        }
    }
}
