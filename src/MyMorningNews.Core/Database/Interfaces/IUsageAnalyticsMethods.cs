namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Aggregations over the model call log for cost and usage analytics.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IUsageAnalyticsMethods
    {
        /// <summary>
        /// Grouped usage and cost report.
        /// </summary>
        /// <param name="filter">Filter; TenantId is required unless AllTenants is set.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Report.</returns>
        Task<UsageReport> ReportAsync(UsageReportFilter filter, CancellationToken token = default);

        /// <summary>
        /// Gap-filled usage and cost time series.
        /// </summary>
        /// <param name="filter">Filter; TenantId is required unless AllTenants is set.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Time series.</returns>
        Task<UsageTimeseries> TimeseriesAsync(UsageReportFilter filter, CancellationToken token = default);
    }
}
