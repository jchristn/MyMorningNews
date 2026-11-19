namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using MyMorningNews.Core.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// User routes: tenant user management (control plane) and the caller's own profile and password.
    /// </summary>
    public class UserRoutes : RouteBase
    {
        /// <summary>
        /// Minimum password length.
        /// </summary>
        public int MinimumPasswordLength { get; set; } = 8;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public UserRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/users", ListAsync, null, openApiMetadata: Paged(Doc("List users", "Users").WithParameter(QueryParam("active", typeof(bool))).WithParameter(QueryParam("isAdmin", typeof(bool))).WithParameter(QueryParam("isTenantAdmin", typeof(bool)))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/users", CreateAsync, null, openApiMetadata: Doc("Create a user", "Users").WithRequestBody(Body<UserCreateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/users/{userId}", ReadAsync, null, openApiMetadata: Doc("Read a user", "Users"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/users/{userId}", UpdateAsync, null, openApiMetadata: Doc("Update a user", "Users").WithRequestBody(Body<UserUpdateRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/users/{userId}", DeleteAsync, null, openApiMetadata: Doc("Delete a user and their credentials", "Users"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/me", MeAsync, null, openApiMetadata: Doc("Read my profile", "Profile"));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/me", UpdateMeAsync, null, openApiMetadata: Doc("Update my profile", "Profile").WithRequestBody(Body<SelfUpdateRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/me/password", ChangePasswordAsync, null, openApiMetadata: Doc("Change my password", "Profile").WithRequestBody(Body<PasswordChangeRequest>()));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserRead).ConfigureAwait(false)) return;
            UserFilter filter = new UserFilter { TenantId = tenantId };
            ApplyPaging(ctx, filter);
            filter.Active = QueryBool(ctx, "active");
            filter.IsAdmin = QueryBool(ctx, "isAdmin");
            filter.IsTenantAdmin = QueryBool(ctx, "isTenantAdmin");
            EnumerationResult<User> users = await Server.Database.Users.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, users.Map(UserResponse.FromModel)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserWrite).ConfigureAwait(false)) return;
            UserCreateRequest? req = await ReadBodyAsync<UserCreateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;

            Dictionary<string, string> errors = new Dictionary<string, string>();
            string email = (req.Email ?? String.Empty).Trim().ToLowerInvariant();
            if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal)) errors["email"] = "A valid email is required.";
            if ((req.Password ?? String.Empty).Length < MinimumPasswordLength) errors["password"] = "Use at least " + MinimumPasswordLength + " characters.";
            string? tzError = TimeZoneHelper.Validate(req.TimeZone);
            if (tzError != null) errors["timeZone"] = tzError;
            if (req.IsAdmin && !Context(ctx).IsAdmin) errors["isAdmin"] = "Only global administrators can create global administrators.";
            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The user is not valid.", errors).ConfigureAwait(false);
                return;
            }

            if (await FindByEmailAsync(tenantId, email, ctx.Token).ConfigureAwait(false) != null)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "A user with that email already exists in the tenant.").ConfigureAwait(false);
                return;
            }

            User user = new User
            {
                TenantId = tenantId,
                Email = email,
                PasswordSha256 = SecretHasher.Sha256Hex(req.Password!),
                FirstName = req.FirstName,
                LastName = req.LastName,
                IsAdmin = req.IsAdmin,
                IsTenantAdmin = req.IsTenantAdmin,
                TimeZone = req.TimeZone,
                Locale = req.Locale,
                Active = req.Active
            };
            user = await Server.Database.Users.CreateAsync(user, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, "User " + user.Id + " created.", 201).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, UserResponse.FromModel(user)).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            string userId = Param(ctx, "userId") ?? String.Empty;
            RequestContext rc = Context(ctx);
            bool self = String.Equals(rc.UserId, userId, StringComparison.Ordinal) && rc.CredentialId == null;
            if (!self && !await AuthorizeAsync(ctx, RequestTypeEnum.UserRead, userId).ConfigureAwait(false)) return;
            User? user = await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            if (user == null)
            {
                await SendNotFoundAsync(ctx, "User").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, UserResponse.FromModel(user)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            string userId = Param(ctx, "userId") ?? String.Empty;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserWrite, userId).ConfigureAwait(false)) return;
            UserUpdateRequest? req = await ReadBodyAsync<UserUpdateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            User? user = await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            if (user == null)
            {
                await SendNotFoundAsync(ctx, "User").ConfigureAwait(false);
                return;
            }

            RequestContext rc = Context(ctx);
            Dictionary<string, string> errors = new Dictionary<string, string>();
            if (req.IsAdmin.HasValue && req.IsAdmin.Value != user.IsAdmin && !rc.IsAdmin) errors["isAdmin"] = "Only global administrators can change global administrator status.";
            if (req.Password != null && req.Password.Length < MinimumPasswordLength) errors["password"] = "Use at least " + MinimumPasswordLength + " characters.";
            string? tzError = TimeZoneHelper.Validate(req.TimeZone);
            if (tzError != null) errors["timeZone"] = tzError;
            if (user.IsProtected && (req.Active == false || req.IsAdmin == false)) errors["active"] = "A protected user cannot be deactivated or demoted.";
            if (String.Equals(rc.UserId, user.Id, StringComparison.Ordinal) && req.Active == false) errors["active"] = "You cannot deactivate yourself.";
            string? newEmail = req.Email?.Trim().ToLowerInvariant();
            if (newEmail != null && newEmail != user.Email)
            {
                if (!newEmail.Contains('@', StringComparison.Ordinal)) errors["email"] = "A valid email is required.";
                else if (await FindByEmailAsync(tenantId, newEmail, ctx.Token).ConfigureAwait(false) != null) errors["email"] = "Another user already uses that email.";
            }

            if (errors.Count > 0)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The update is not valid.", errors).ConfigureAwait(false);
                return;
            }

            if (newEmail != null) user.Email = newEmail;
            if (req.Password != null) user.PasswordSha256 = SecretHasher.Sha256Hex(req.Password);
            if (req.FirstName != null) user.FirstName = req.FirstName;
            if (req.LastName != null) user.LastName = req.LastName;
            if (req.IsAdmin.HasValue) user.IsAdmin = req.IsAdmin.Value;
            if (req.IsTenantAdmin.HasValue) user.IsTenantAdmin = req.IsTenantAdmin.Value;
            if (req.TimeZone != null) user.TimeZone = req.TimeZone;
            if (req.Locale != null) user.Locale = req.Locale;
            if (req.Active.HasValue) user.Active = req.Active.Value;
            user = await Server.Database.Users.UpdateAsync(user, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, "User " + user.Id + " updated.", 200).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, UserResponse.FromModel(user)).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            string? tenantId = await ResolveTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            string userId = Param(ctx, "userId") ?? String.Empty;
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserDelete, userId).ConfigureAwait(false)) return;
            User? user = await Server.Database.Users.ReadAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            if (user == null)
            {
                await SendNotFoundAsync(ctx, "User").ConfigureAwait(false);
                return;
            }

            if (user.IsProtected)
            {
                await SendErrorAsync(ctx, 409, "Conflict", "A protected user cannot be deleted.").ConfigureAwait(false);
                return;
            }

            if (String.Equals(Context(ctx).UserId, user.Id, StringComparison.Ordinal))
            {
                await SendErrorAsync(ctx, 409, "Conflict", "You cannot delete yourself.").ConfigureAwait(false);
                return;
            }

            await Server.Database.Users.DeleteAsync(tenantId, userId, ctx.Token).ConfigureAwait(false);
            await ChangedAsync(ctx, "User " + userId + " deleted.", 204).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task MeAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserSelfRead).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            if (rc.Authentication.User == null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "The administration key has no user profile.").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, UserResponse.FromModel(rc.Authentication.User)).ConfigureAwait(false);
        }

        private async Task UpdateMeAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserSelfWrite).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            if (rc.Authentication.User == null || rc.CredentialId != null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Only interactive users can update their profile.").ConfigureAwait(false);
                return;
            }

            SelfUpdateRequest? req = await ReadBodyAsync<SelfUpdateRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string? tzError = TimeZoneHelper.Validate(req.TimeZone);
            if (tzError != null)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", tzError, new Dictionary<string, string> { { "timeZone", tzError } }).ConfigureAwait(false);
                return;
            }

            User user = rc.Authentication.User;
            if (req.FirstName != null) user.FirstName = req.FirstName;
            if (req.LastName != null) user.LastName = req.LastName;
            if (req.TimeZone != null) user.TimeZone = req.TimeZone;
            if (req.Locale != null) user.Locale = req.Locale;
            user = await Server.Database.Users.UpdateAsync(user, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, UserResponse.FromModel(user)).ConfigureAwait(false);
        }

        private async Task ChangePasswordAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.UserSelfWrite).ConfigureAwait(false)) return;
            RequestContext rc = Context(ctx);
            if (rc.Authentication.User == null || rc.CredentialId != null)
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Only interactive users can change their password.").ConfigureAwait(false);
                return;
            }

            PasswordChangeRequest? req = await ReadBodyAsync<PasswordChangeRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            User user = rc.Authentication.User;
            if (!SecretHasher.VerifyPassword(req.CurrentPassword, user.PasswordSha256))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "The current password is incorrect.", new Dictionary<string, string> { { "currentPassword", "Incorrect." } }).ConfigureAwait(false);
                return;
            }

            if ((req.NewPassword ?? String.Empty).Length < MinimumPasswordLength)
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "Use at least " + MinimumPasswordLength + " characters.", new Dictionary<string, string> { { "newPassword", "Too short." } }).ConfigureAwait(false);
                return;
            }

            user.PasswordSha256 = SecretHasher.Sha256Hex(req.NewPassword!);
            await Server.Database.Users.UpdateAsync(user, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.UserChanged, rc, 204, "Password changed.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task<User?> FindByEmailAsync(string tenantId, string email, CancellationToken token)
        {
            EnumerationResult<User> found = await Server.Database.Users.EnumerateAsync(new UserFilter { TenantId = tenantId, Email = email, PageSize = 1 }, token).ConfigureAwait(false);
            return found.Items.Count > 0 ? found.Items[0] : null;
        }

        private async Task ChangedAsync(HttpContextBase ctx, string description, int status)
        {
            Server.Authorization.Invalidate();
            MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.RbacMutations, "mutation", "user_changed");
            await Server.Audit.RecordAsync(AuditEventTypeEnum.UserChanged, Context(ctx), status, description, ctx.Token).ConfigureAwait(false);
        }
    }
}
