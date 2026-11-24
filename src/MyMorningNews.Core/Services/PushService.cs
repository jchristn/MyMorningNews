namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Sends push notifications to a user's registered devices through the Expo Push API and prunes devices Expo reports
    /// as unregistered.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class PushService : IDisposable
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _Http;
        private readonly string _Header = "[PushService] ";
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="settings">Server settings (reads the Push group on every send, so changes apply live).</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PushService(DatabaseDriverBase database, ServerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send a message to every device of a user.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result; zero accepted when push is disabled or the user has no devices.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task<PushResult> SendToUserAsync(string tenantId, string userId, PushMessage message, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            ArgumentNullException.ThrowIfNull(message);
            EnumerationResult<Device> devices = await _Database.Devices.EnumerateAsync(new DeviceFilter { TenantId = tenantId, UserId = userId, PageSize = 100 }, token).ConfigureAwait(false);
            return await SendAsync(devices.Objects, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a message to specific devices.
        /// </summary>
        /// <param name="devices">Devices.</param>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public async Task<PushResult> SendAsync(List<Device> devices, PushMessage message, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(devices);
            ArgumentNullException.ThrowIfNull(message);
            PushResult result = new PushResult();
            if (!_Settings.Push.Enabled || !devices.Any()) return result;

            List<Dictionary<string, object>> payload = devices.Select(d => new Dictionary<string, object>
            {
                { "to", d.ExpoPushToken },
                { "title", message.Title },
                { "body", message.Body },
                { "sound", "default" },
                { "channelId", message.ChannelId },
                { "data", message.Data }
            }).ToList();

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _Settings.Push.ExpoPushUrl))
            {
                timeout.CancelAfter(_Settings.Push.TimeoutMs);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                if (!String.IsNullOrEmpty(_Settings.Push.ExpoAccessToken)) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _Settings.Push.ExpoAccessToken);
                try
                {
                    using (HttpResponseMessage response = await _Http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            result.Failed = devices.Count;
                            result.Errors.Add("Expo push returned " + (int)response.StatusCode + ".");
                            _Logging.Warn(_Header + "push request failed with " + (int)response.StatusCode);
                        }
                        else
                        {
                            await ApplyTicketsAsync(devices, body, result, token).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                {
                    result.Failed = devices.Count;
                    result.Errors.Add("Push service unreachable: " + ex.Message);
                    _Logging.Warn(_Header + "push service unreachable: " + ex.Message);
                }
            }

            MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.PushMessages, result.Accepted, "outcome", "accepted");
            if (result.Failed > 0) MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.PushMessages, result.Failed, "outcome", "failed");
            return result;
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Http.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task ApplyTicketsAsync(List<Device> devices, string body, PushResult result, CancellationToken token)
        {
            using (JsonDocument doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(body) ? "{}" : body))
            {
                if (!doc.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
                {
                    result.Accepted = devices.Count;
                    return;
                }

                int i = 0;
                foreach (JsonElement ticket in data.EnumerateArray())
                {
                    if (i >= devices.Count) break;
                    Device device = devices[i++];
                    string status = ticket.TryGetProperty("status", out JsonElement s) ? s.GetString() ?? String.Empty : String.Empty;
                    if (String.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Accepted++;
                        device.LastSeenUtc = DateTime.UtcNow;
                        device.LastError = null;
                        await _Database.Devices.UpdateAsync(device, token).ConfigureAwait(false);
                        continue;
                    }

                    string error = ticket.TryGetProperty("details", out JsonElement details) && details.TryGetProperty("error", out JsonElement e) ? e.GetString() ?? String.Empty : String.Empty;
                    string text = ticket.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? error : error;
                    if (String.Equals(error, "DeviceNotRegistered", StringComparison.Ordinal))
                    {
                        result.Removed++;
                        await _Database.Devices.DeleteAsync(device.TenantId, device.Id, token).ConfigureAwait(false);
                    }
                    else
                    {
                        result.Failed++;
                        result.Errors.Add(text);
                        device.LastError = text;
                        await _Database.Devices.UpdateAsync(device, token).ConfigureAwait(false);
                    }
                }
            }
        }

        #endregion
    }
}
