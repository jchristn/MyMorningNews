namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Helpers;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of <see cref="IPlatformStatsMethods"/>.
    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.
    /// </summary>
    public class PlatformStatsMethods : PostgresqlMethodsBase, IPlatformStatsMethods
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">PostgreSQL driver.</param>
        public PlatformStatsMethods(PostgresqlDatabaseDriver driver) : base(driver)
        {
        }

        /// <inheritdoc />
        public async Task TouchAsync(string tenantId, string userId, DateTime utcNow, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            DateTime now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            await ExecuteAsync(PlatformStatsQueries.Touch, token, IdGenerator.GenerateUserActivityDayId(), tenantId, userId, DateOnly.FromDateTime(now), now).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> PruneActivityAsync(DateTime beforeUtc, CancellationToken token = default)
        {
            return await ExecuteAsync(PlatformStatsQueries.Prune, token, DateOnly.FromDateTime(beforeUtc)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<PlatformDailyStats> ReadAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default)
        {
            if (toUtc <= fromUtc) throw new ArgumentException("toUtc must be after fromUtc.", nameof(toUtc));
            DateTime from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
            DateTime to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
            List<PlatformDailyStats> rows = await QueryAsync(PlatformStatsQueries.Daily, MapDaily, token, from, to, to.AddDays(-7), to.AddDays(-30)).ConfigureAwait(false);
            PlatformDailyStats stats = rows.FirstOrDefault() ?? new PlatformDailyStats();
            stats.FromUtc = from;
            stats.ToUtc = to;
            stats.TopModels = await QueryAsync(PlatformStatsQueries.TopModels, r => new PlatformModelUsage
            {
                Provider = r.GetString(0),
                Model = r.GetString(1),
                Calls = r.GetInt64(2),
                Tokens = Convert.ToInt64(r.GetValue(3)),
                CostUsd = Convert.ToDecimal(r.GetValue(4))
            }, token, from, to).ConfigureAwait(false);
            return stats;
        }

        private static PlatformDailyStats MapDaily(NpgsqlDataReader r)
        {
            int i = 0;
            long L() { return Convert.ToInt64(r.GetValue(i++)); }
            double D() { return Convert.ToDouble(r.GetValue(i++)); }
            decimal M() { return Convert.ToDecimal(r.GetValue(i++)); }
            PlatformDailyStats s = new PlatformDailyStats();
            s.TotalUsers = L();
            s.NewUsers = L();
            s.NewSignups = L();
            s.EmailsVerified = L();
            s.DeletionRequests = L();
            s.DailyActiveUsers = L();
            s.WeeklyActiveUsers = L();
            s.MonthlyActiveUsers = L();
            s.SignIns = L();
            s.ActiveTopics = L();
            s.NewTopics = L();
            s.RunsCompleted = L();
            s.RunsFailed = L();
            s.RunsSkipped = L();
            s.NewFindings = L();
            s.AverageRunSeconds = D() / 1000.0;
            s.BriefingsReady = L();
            s.ModelCalls = L();
            s.ModelFailures = L();
            s.InputTokens = L();
            s.OutputTokens = L();
            s.ModelCostUsd = M();
            s.ModelMinutes = D() / 60000.0;
            s.ModelCostUsd30Days = M();
            s.AudioFiles = L();
            s.AudioMinutes = D() / 60.0;
            s.EmailsSent = L();
            s.EmailsFailed = L();
            s.PushDevices = L();
            s.NewPushDevices = L();
            s.PaidSubscriptions = L();
            s.NewPaidSubscriptions = L();
            s.CatalogSubscriptions = L();
            s.StalePrompts = L();
            s.StaleKept = L();
            s.StaleDeleted = L();
            return s;
        }
    }
}
