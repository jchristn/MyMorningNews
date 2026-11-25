namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Requests;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// The daily platform summary for global administrators: preview the numbers, or send the email now.
    /// </summary>
    public class AdminReportRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public AdminReportRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/admin/reports/daily";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ReadAsync, null, openApiMetadata: Doc("Platform statistics for one UTC day", "Analytics",
                "Global administrators only. day is a UTC date (yyyy-MM-dd); the default is yesterday.").WithParameter(QueryParam("day", typeof(DateTime))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/send", SendAsync, null, openApiMetadata: Doc("Send the daily summary email now", "Analytics",
                "Global administrators only. Uses adminReports.recipients unless recipients is given. Each day is sent at most once per address; returns the number queued.")
                .WithRequestBody(Body<AdminReportSendRequest>()));
        }

        private async Task<bool> AdminAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.SettingsRead).ConfigureAwait(false)) return false;
            if (Context(ctx).IsAdmin) return true;
            await SendErrorAsync(ctx, 403, "Forbidden", "Only global administrators read platform statistics.").ConfigureAwait(false);
            return false;
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            DateTime day = (QueryDate(ctx, "day") ?? DateTime.UtcNow.Date.AddDays(-1)).Date;
            PlatformDailyStats stats = await Server.Database.PlatformStats.ReadAsync(day, day.AddDays(1), ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, stats).ConfigureAwait(false);
        }

        private async Task SendAsync(HttpContextBase ctx)
        {
            if (!await AdminAsync(ctx).ConfigureAwait(false)) return;
            AdminReportSendRequest request = await ReadOptionalBodyAsync<AdminReportSendRequest>(ctx).ConfigureAwait(false) ?? new AdminReportSendRequest();
            DateTime day = (request.DayUtc ?? DateTime.UtcNow.Date.AddDays(-1)).Date;
            List<string> recipients = request.Recipients != null && request.Recipients.Count > 0 ? request.Recipients : Server.Settings.AdminReports.Recipients;
            if (recipients.Count == 0) { await SendErrorAsync(ctx, 400, "ValidationFailed", "No recipients. Set adminReports.recipients or pass recipients.").ConfigureAwait(false); return; }
            int queued = await Server.AdminReports.SendAsync(day, recipients, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new Dictionary<string, object> { { "queued", queued }, { "day", day.ToString("yyyy-MM-dd") } }).ConfigureAwait(false);
        }
    }
}
