namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Analytics reports and usage (mine, report, time series), with a typed error for an invalid grouping.
    /// </summary>
    public class AnalyticsUsageTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Analytics reports and usage";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AnalyticsUsageTest()
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
            AnalyticsOptions scope = new AnalyticsOptions { FromUtc = DateTime.UtcNow.AddDays(-30), ToUtc = DateTime.UtcNow };
            await StepAsync(context, result, "GET analytics/relevance", () => c.Analytics.RelevanceAsync(scope, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET analytics/accuracy", () => c.Analytics.AccuracyAsync(scope, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET analytics/feedback", () => c.Analytics.FeedbackAsync(scope, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET analytics/suggestions", () => c.Analytics.SuggestionsAsync(scope, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET analytics/reasons", () => c.Analytics.ReasonsAsync(scope, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET analytics/quality", () => c.Analytics.QualityAsync(new AnalyticsOptions { All = true }, token)).ConfigureAwait(false);
            await StepAsync(context, result, "GET analytics/relevance for an unknown topic (404)", () => Helpers.ExpectErrorAsync(() => c.Analytics.RelevanceAsync(new AnalyticsOptions { TopicId = "top_missing" }, token), 404)).ConfigureAwait(false);

            UsageReport mine = await StepAsync(context, result, "GET usage/me", () => c.Usage.GetMineAsync(null, token)).ConfigureAwait(false);
            Helpers.Equal(UsageGroupByEnum.Task, mine.GroupBy, "usage/me groups by task");
            UsageReport report = await StepAsync(context, result, "GET usage/report (by model)", () => c.Usage.GetReportAsync(new UsageOptions { GroupBy = UsageGroupByEnum.Model, Top = 10 }, token)).ConfigureAwait(false);
            Helpers.Equal(UsageGroupByEnum.Model, report.GroupBy, "report grouping");
            UsageTimeseries series = await StepAsync(context, result, "GET usage/timeseries", () => c.Usage.GetTimeseriesAsync(new UsageOptions { FromUtc = DateTime.UtcNow.AddDays(-7), BucketMinutes = 1440 }, token)).ConfigureAwait(false);
            Helpers.Equal(1440, series.BucketMinutes, "bucket size");
            Helpers.True(series.Buckets.Count >= 7, "time series is gap-filled");
            Helpers.True(series.GroupBy == null && series.Groups.Count == 0 && series.Buckets.All(b => b.Rows.Count == 0), "a plain series is not grouped");
            UsageTimeseries grouped = await StepAsync(context, result, "GET usage/timeseries (by model)", () => c.Usage.GetTimeseriesAsync(new UsageOptions { FromUtc = DateTime.UtcNow.AddDays(-7), BucketMinutes = 1440, GroupBy = UsageGroupByEnum.Model, Top = 5 }, token)).ConfigureAwait(false);
            Helpers.True(grouped.GroupBy == UsageGroupByEnum.Model, "grouped series");
            Helpers.True(grouped.Groups.All(g => g.Key == "(other)" || (g.Key.Contains('|') && g.Model != null)), "model groups are keyed by endpoint and model");
            Helpers.True(grouped.Buckets.Sum(b => b.Rows.Sum(r => r.Calls)) == grouped.Buckets.Sum(b => b.Calls), "rows add up to the buckets");
            await StepAsync(context, result, "GET usage/timeseries groupBy=Day (400)", () => Helpers.ExpectErrorAsync(() => c.Usage.GetTimeseriesAsync(new UsageOptions { GroupBy = UsageGroupByEnum.Day }, token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "GET usage/report groupBy=Tenant without allTenants (400)", () => Helpers.ExpectErrorAsync(() => c.Usage.GetReportAsync(new UsageOptions { GroupBy = UsageGroupByEnum.Tenant }, token), 400)).ConfigureAwait(false);
        }
    }
}
