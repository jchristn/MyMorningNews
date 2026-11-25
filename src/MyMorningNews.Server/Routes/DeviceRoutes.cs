namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// The caller's mobile devices for push notifications (plan section 35.5).
    /// </summary>
    public class DeviceRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public DeviceRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/devices", ListAsync, null, openApiMetadata: Paged(Doc("List my push devices", "Devices")));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/devices" + "/enumerate", ctx => EnumerateAsync(ctx, ListAsync), null, openApiMetadata: Paged(Doc("List my push devices", "Devices")).WithRequestBody(Body<EnumerationQuery>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/devices", RegisterAsync, null, openApiMetadata: Doc("Register a device for push notifications", "Devices",
                "Returns 201 for a new device, or 200 with the existing device when the same token is registered again. Push is a choice per kind of notification " +
                "(PUT /v1.0/api/notifications/preferences): save the kinds to push before registering. A user who registers a device without any push choice gets the kinds " +
                "default push kinds: RunResults, BriefingReady, RunFailed, CapReached, PredictionDue, and TopicStale.").WithRequestBody(Body<DeviceRegisterRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/devices/{id}", DeleteAsync, null, openApiMetadata: Doc("Remove a device", "Devices"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/devices/{id}/test", TestAsync, null, openApiMetadata: Doc("Send a test notification to a device", "Devices"));
        }

        private async Task<string?> RequireUserAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            RequestContext rc = Context(ctx);
            if (String.IsNullOrEmpty(rc.UserId) || String.IsNullOrEmpty(rc.TenantId))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Devices belong to a signed-in user.").ConfigureAwait(false);
                return null;
            }

            return rc.UserId;
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            string? userId = await RequireUserAsync(ctx, RequestTypeEnum.DeviceRead).ConfigureAwait(false);
            if (userId == null) return;
            DeviceFilter query = new DeviceFilter { TenantId = Context(ctx).TenantId, UserId = userId };
            ApplyPaging(ctx, query);
            await SendJsonAsync(ctx, 200, await Server.Database.Devices.EnumerateAsync(query, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RegisterAsync(HttpContextBase ctx)
        {
            string? userId = await RequireUserAsync(ctx, RequestTypeEnum.DeviceWrite).ConfigureAwait(false);
            if (userId == null) return;
            DeviceRegisterRequest? req = await ReadBodyAsync<DeviceRegisterRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string platform = (req.Platform ?? String.Empty).Trim().ToLowerInvariant();
            string pushToken = (req.ExpoPushToken ?? String.Empty).Trim();
            if (platform != "ios" && platform != "android")
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "platform must be ios or android.").ConfigureAwait(false);
                return;
            }

            if (pushToken.Length < 10 || pushToken.Length > 256 || !(pushToken.StartsWith("ExponentPushToken[", StringComparison.Ordinal) || pushToken.StartsWith("ExpoPushToken[", StringComparison.Ordinal)))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "expoPushToken must be an Expo push token.").ConfigureAwait(false);
                return;
            }

            string tenantId = Context(ctx).TenantId!;
            EnumerationResult<Device> existing = await Server.Database.Devices.EnumerateAsync(new DeviceFilter { TenantId = tenantId, UserId = userId, ExpoPushToken = pushToken, PageSize = 1 }, ctx.Token).ConfigureAwait(false);
            Device? device = existing.Objects.FirstOrDefault();
            if (device != null)
            {
                device.Platform = platform;
                device.Name = Trim(req.Name, 128) ?? device.Name;
                device.Locale = Trim(req.Locale, 32) ?? device.Locale;
                device.LastSeenUtc = DateTime.UtcNow;
                device = await Server.Database.Devices.UpdateAsync(device, ctx.Token).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, device).ConfigureAwait(false);
                return;
            }

            device = await Server.Database.Devices.CreateAsync(new Device
            {
                TenantId = tenantId,
                UserId = userId,
                ExpoPushToken = pushToken,
                Platform = platform,
                Name = Trim(req.Name, 128),
                Locale = Trim(req.Locale, 32),
                LastSeenUtc = DateTime.UtcNow
            }, ctx.Token).ConfigureAwait(false);
            // App builds from before per-kind preferences register a device and expect pushes; a user who has made no
            // push choice yet keeps the kinds those builds delivered. Newer builds save the chosen kind first.
            await Server.Notifications.Preferences.KeepEarlierPushAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, device).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            string? userId = await RequireUserAsync(ctx, RequestTypeEnum.DeviceWrite).ConfigureAwait(false);
            if (userId == null) return;
            Device? device = await OwnedAsync(ctx, userId).ConfigureAwait(false);
            if (device == null) return;
            await Server.Database.Devices.DeleteAsync(device.TenantId, device.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task TestAsync(HttpContextBase ctx)
        {
            string? userId = await RequireUserAsync(ctx, RequestTypeEnum.DeviceWrite).ConfigureAwait(false);
            if (userId == null) return;
            Device? device = await OwnedAsync(ctx, userId).ConfigureAwait(false);
            if (device == null) return;
            PushResult result = await Server.Push.SendAsync(new List<Device> { device }, new PushMessage
            {
                Title = "My Morning News",
                Body = "Notifications are working on this device.",
                ChannelId = "account",
                Data = new Dictionary<string, string> { { "kind", "test" } }
            }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private async Task<Device?> OwnedAsync(HttpContextBase ctx, string userId)
        {
            string? id = Param(ctx, "id");
            Device? device = String.IsNullOrEmpty(id) ? null : await Server.Database.Devices.ReadAsync(Context(ctx).TenantId!, id, ctx.Token).ConfigureAwait(false);
            if (device == null || !String.Equals(device.UserId, userId, StringComparison.Ordinal))
            {
                await SendNotFoundAsync(ctx, "Device").ConfigureAwait(false);
                return null;
            }

            return device;
        }

        private static string? Trim(string? value, int max)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            string t = value.Trim();
            return t.Length > max ? t.Substring(0, max) : t;
        }
    }
}
