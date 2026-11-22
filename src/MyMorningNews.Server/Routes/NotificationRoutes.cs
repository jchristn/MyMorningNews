namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// The caller's in-app notifications (list, unread count, mark read, delete) and notification preferences: which
    /// kinds of notification they want on which channel (email, push, in-app). Every route acts on the signed-in user
    /// only.
    /// </summary>
    public class NotificationRoutes : RouteBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public NotificationRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            const string b = "/v1.0/api/notifications";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b, ListAsync, null, openApiMetadata: Paged(Doc("List my in-app notifications", "Notifications",
                "Newest first. A notification is created only for kinds the user turned on for the in-app channel. Rows older than 90 days are removed.")
                .WithParameter(QueryParam("unread", typeof(bool))).WithParameter(QueryParam("kind", typeof(NotificationKindEnum)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b + "/unread-count", UnreadCountAsync, null, openApiMetadata: Doc("Count my unread in-app notifications", "Notifications"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, b + "/read-all", ReadAllAsync, null, openApiMetadata: Doc("Mark all my in-app notifications as read", "Notifications",
                "Returns how many were marked."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, b + "/preferences", PreferencesAsync, null, openApiMetadata: Doc("My notification preferences", "Notifications",
                "Every kind on every channel, and whether each channel can deliver right now (a usable email address; at least one registered device for push). " +
                "Without a saved choice the default applies: email is on for RunResults, BriefingReady, RunFailed, and CapReached; everything else is off."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, b + "/preferences", UpdatePreferencesAsync, null, openApiMetadata: Doc("Change my notification preferences", "Notifications",
                "Only the cells that are sent change. A push cell can be saved before a device is registered. Topic lists stop or restore run emails for single topics.")
                .WithRequestBody(Body<NotificationPreferencesRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, b + "/{id}/read", ReadAsync, null, openApiMetadata: Doc("Mark an in-app notification as read", "Notifications"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, b + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete an in-app notification", "Notifications"));
        }

        private async Task<User?> RequireUserAsync(HttpContextBase ctx, RequestTypeEnum type)
        {
            if (!await AuthorizeAsync(ctx, type).ConfigureAwait(false)) return null;
            RequestContext rc = Context(ctx);
            User? me = String.IsNullOrEmpty(rc.UserId) || String.IsNullOrEmpty(rc.TenantId) ? null : await Server.Database.Users.ReadAsync(rc.TenantId, rc.UserId, ctx.Token).ConfigureAwait(false);
            if (me == null) await SendErrorAsync(ctx, 400, "BadRequest", "Notifications belong to a signed-in user.").ConfigureAwait(false);
            return me;
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationRead).ConfigureAwait(false);
            if (me == null) return;
            NotificationFilter filter = new NotificationFilter { TenantId = me.TenantId };
            ApplyPaging(ctx, filter);
            // Set after paging so the query string can never widen the list beyond the caller's own rows.
            filter.TenantId = me.TenantId;
            filter.UserId = me.Id;
            filter.Unread = QueryBool(ctx, "unread");
            filter.Kind = QueryEnum<NotificationKindEnum>(ctx, "kind");
            await SendJsonAsync(ctx, 200, await Server.Database.Notifications.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UnreadCountAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationRead).ConfigureAwait(false);
            if (me == null) return;
            long count = await Server.Database.Notifications.CountAsync(new NotificationFilter { TenantId = me.TenantId, UserId = me.Id, Unread = true }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new NotificationCountResponse { Count = count }).ConfigureAwait(false);
        }

        private async Task ReadAllAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationWrite).ConfigureAwait(false);
            if (me == null) return;
            int marked = await Server.Database.Notifications.MarkAllReadAsync(me.TenantId, me.Id, DateTime.UtcNow, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new NotificationCountResponse { Count = marked }).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationWrite).ConfigureAwait(false);
            if (me == null) return;
            Notification? n = await OwnedAsync(ctx, me).ConfigureAwait(false);
            if (n == null) return;
            if (!n.ReadUtc.HasValue)
            {
                n.ReadUtc = DateTime.UtcNow;
                n = await Server.Database.Notifications.UpdateAsync(n, ctx.Token).ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 200, n).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationWrite).ConfigureAwait(false);
            if (me == null) return;
            Notification? n = await OwnedAsync(ctx, me).ConfigureAwait(false);
            if (n == null) return;
            await Server.Database.Notifications.DeleteAsync(n.TenantId, n.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task PreferencesAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationRead).ConfigureAwait(false);
            if (me == null) return;
            await SendJsonAsync(ctx, 200, await Server.Notifications.DescribeAsync(me, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpdatePreferencesAsync(HttpContextBase ctx)
        {
            User? me = await RequireUserAsync(ctx, RequestTypeEnum.NotificationWrite).ConfigureAwait(false);
            if (me == null) return;
            NotificationPreferencesRequest? req = await ReadBodyAsync<NotificationPreferencesRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            await SendJsonAsync(ctx, 200, await Server.Notifications.UpdateAsync(me, req, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task<Notification?> OwnedAsync(HttpContextBase ctx, User me)
        {
            string? id = Param(ctx, "id");
            Notification? n = String.IsNullOrEmpty(id) ? null : await Server.Database.Notifications.ReadAsync(me.TenantId, id, ctx.Token).ConfigureAwait(false);
            if (n == null || !String.Equals(n.UserId, me.Id, StringComparison.Ordinal))
            {
                await SendNotFoundAsync(ctx, "Notification").ConfigureAwait(false);
                return null;
            }

            return n;
        }
    }
}
