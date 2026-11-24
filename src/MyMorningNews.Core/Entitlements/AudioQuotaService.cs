namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Settings;

    /// <summary>Daily UTC generated-audio reservations and exact settlement. Retries use the same operation and media deletion never refunds consumption.</summary>
    public class AudioQuotaService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly EntitlementService _Entitlements;

        /// <summary>Create a quota service with the server's database and settings.</summary>
        public AudioQuotaService(DatabaseDriverBase database, ServerSettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Entitlements = new EntitlementService(database, settings);
        }

        /// <summary>Hold estimated duration plus headroom before generation. Throws CapReachedException if audio is disabled or capacity is unavailable.</summary>
        public async Task<AudioReservation> ReserveAsync(MediaAsset asset, int targetMinutes, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            EffectivePlan plan = await _Entitlements.GetEffectiveAsync(asset.TenantId, asset.UserId, token).ConfigureAwait(false);
            if (!EntitlementService.Allowed(plan, "audio.enabled")) throw new CapReachedException("audio.enabled", "false", null, "Custom audio generation is not included in your plan. Shared catalog audio remains available.");
            long? maximum = (long?)EntitlementService.Limit(plan, "audio.dailySeconds.max");
            long seconds = checked((long)Math.Clamp(targetMinutes, 1, 240) * 75);
            if (maximum.HasValue) seconds = Math.Min(seconds, maximum.Value);
            if (seconds < 1) throw Full(maximum);
            DateTime now = DateTime.UtcNow;
            AudioReservation? reservation = await _Database.AudioReservations.TryReserveAsync(new AudioReservation
            {
                TenantId = asset.TenantId, UserId = asset.UserId, OperationId = asset.Id, PeriodStartUtc = now.Date,
                ReservedSeconds = seconds, ExpiresUtc = now.AddHours(2)
            }, maximum, token).ConfigureAwait(false);
            return reservation ?? throw Full(maximum);
        }

        /// <summary>Atomically admit a queued episode and its reservation. Duplicate calls reuse the episode.</summary>
        public async Task<MediaAsset> QueueAsync(MediaAsset asset, int targetMinutes, bool force, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            long? maximum = await MaximumAsync(asset.TenantId, asset.UserId, token).ConfigureAwait(false);
            long seconds = checked((long)Math.Clamp(targetMinutes, 1, 240) * 75);
            if (maximum.HasValue) seconds = Math.Min(seconds, maximum.Value);
            if (seconds < 1) throw Full(maximum);
            DateTime now = DateTime.UtcNow;
            return await _Database.AudioReservations.TryQueueAsync(asset, new AudioReservation
            {
                TenantId = asset.TenantId, UserId = asset.UserId, OperationId = asset.Id, PeriodStartUtc = now.Date,
                ReservedSeconds = seconds, ExpiresUtc = now.AddHours(2)
            }, maximum, force, token).ConfigureAwait(false) ?? throw Full(maximum);
        }

        /// <summary>Reserve a bounded voice sample against the same daily allowance as complete episodes.</summary>
        public async Task<AudioReservation> ReserveSampleAsync(string tenantId, string userId, string operationId, int estimatedSeconds, CancellationToken token = default)
        {
            long? maximum = await MaximumAsync(tenantId, userId, token).ConfigureAwait(false);
            long seconds = Math.Clamp(estimatedSeconds, 1, 600);
            if (maximum.HasValue) seconds = Math.Min(seconds, maximum.Value);
            if (seconds < 1) throw Full(maximum);
            return await _Database.AudioReservations.TryReserveAsync(new AudioReservation
            {
                TenantId = tenantId, UserId = userId, OperationId = operationId, PeriodStartUtc = DateTime.UtcNow.Date,
                ReservedSeconds = seconds, ExpiresUtc = DateTime.UtcNow.AddHours(2)
            }, maximum, token).ConfigureAwait(false) ?? throw Full(maximum);
        }

        /// <summary>Record measured sample duration before returning audio. Charges cannot be removed by replay or deletion.</summary>
        public async Task CommitSampleAsync(AudioReservation reservation, double seconds, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(reservation);
            if (!Double.IsFinite(seconds) || seconds <= 0 || seconds > 600) throw new ArgumentOutOfRangeException(nameof(seconds));
            long? maximum = await MaximumAsync(reservation.TenantId, reservation.UserId, token).ConfigureAwait(false);
            if (await _Database.AudioReservations.TryCommitSampleAsync(reservation.TenantId, reservation.UserId, reservation.OperationId,
                checked((long)Math.Ceiling(seconds * 1000)), maximum, token).ConfigureAwait(false) == null) throw Full(maximum);
        }

        private async Task<long?> MaximumAsync(string tenantId, string userId, CancellationToken token)
        {
            EffectivePlan plan = await _Entitlements.GetEffectiveAsync(tenantId, userId, token).ConfigureAwait(false);
            if (!EntitlementService.Allowed(plan, "audio.enabled")) throw new CapReachedException("audio.enabled", "false", null, "Custom audio generation is not included in your plan. Shared catalog audio remains available.");
            return (long?)EntitlementService.Limit(plan, "audio.dailySeconds.max");
        }

        /// <summary>Publish and charge one measured episode atomically. A failed overrun remains unpublished and does not consume a successful-generation allowance.</summary>
        public async Task CommitAsync(MediaAsset asset, double seconds, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (!Double.IsFinite(seconds) || seconds <= 0 || seconds > 86400) throw new ArgumentOutOfRangeException(nameof(seconds));
            EffectivePlan plan = await _Entitlements.GetEffectiveAsync(asset.TenantId, asset.UserId, token).ConfigureAwait(false);
            if (!EntitlementService.Allowed(plan, "audio.enabled")) throw new CapReachedException("audio.enabled", "false", null, "Audio access changed before this episode finished.");
            long? maximum = (long?)EntitlementService.Limit(plan, "audio.dailySeconds.max");
            AudioReservation? committed = await _Database.AudioReservations.TryCommitAsync(asset, checked((long)Math.Ceiling(seconds * 1000)), maximum, token).ConfigureAwait(false);
            if (committed == null) throw Full(maximum);
        }

        private static CapReachedException Full(long? maximum)
        {
            return new CapReachedException("audio.dailySeconds.max", maximum?.ToString(CultureInfo.InvariantCulture) ?? "unlimited", DateTime.UtcNow.Date.AddDays(1),
                "There is not enough generated-audio time available today. Existing audio can still be replayed, and text briefings remain available. The allowance resets at midnight UTC.");
        }
    }
}
