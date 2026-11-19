namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using SyslogLogging;

    /// <summary>
    /// Idempotent first-boot seeding: default tenant, default administrator, and built-in roles. Built-in role definitions
    /// are refreshed on every start while their identifiers are preserved, so existing assignments keep resolving.
    /// Thread safety: run once at startup.
    /// </summary>
    public class SeedService
    {
        #region Public-Members

        /// <summary>
        /// Default tenant identifier. Default "default".
        /// </summary>
        public string DefaultTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Default administrator email. Default admin@mymorningnews.
        /// </summary>
        public string DefaultAdminEmail { get; set; } = "admin@mymorningnews";

        /// <summary>
        /// Default administrator password. Default "password". Change it after first login.
        /// </summary>
        public string DefaultAdminPassword { get; set; } = "password";

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[SeedService] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SeedService(DatabaseDriverBase database, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(logging);
            _Database = database;
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Seed and refresh.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task EnsureAsync(CancellationToken token = default)
        {
            await EnsureDefaultTenantAsync(token).ConfigureAwait(false);
            await EnsureDefaultAdminAsync(token).ConfigureAwait(false);
            await RefreshBuiltInRolesAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task EnsureDefaultTenantAsync(CancellationToken token)
        {
            if (await _Database.Tenants.ExistsAsync(DefaultTenantId, token).ConfigureAwait(false)) return;
            await _Database.Tenants.CreateAsync(new Tenant { Id = DefaultTenantId, Name = "Default", IsProtected = true, TimeZone = "UTC" }, token).ConfigureAwait(false);
            _Logging.Info(_Header + "created default tenant " + DefaultTenantId);
        }

        private async Task EnsureDefaultAdminAsync(CancellationToken token)
        {
            long users = await _Database.Users.CountAsync(new UserFilter { TenantId = DefaultTenantId }, token).ConfigureAwait(false);
            if (users > 0) return;
            await _Database.Users.CreateAsync(new User
            {
                TenantId = DefaultTenantId,
                FirstName = "Default",
                LastName = "Administrator",
                Email = DefaultAdminEmail,
                PasswordSha256 = SecretHasher.Sha256Hex(DefaultAdminPassword),
                IsAdmin = true,
                IsTenantAdmin = true,
                IsProtected = true
            }, token).ConfigureAwait(false);
            _Logging.Warn(_Header + "created default administrator " + DefaultAdminEmail + " in tenant " + DefaultTenantId + "; change the default password");
        }

        private async Task RefreshBuiltInRolesAsync(CancellationToken token)
        {
            foreach (BuiltInRoleDefinition def in BuiltInRoles.All)
            {
                EnumerationResult<UserRole> existing = await _Database.Roles.EnumerateAsync(new UserRoleFilter { TenantId = null, Name = def.Name, PageSize = 2 }, token).ConfigureAwait(false);
                UserRole? role = existing.Items.FirstOrDefault();
                if (role == null)
                {
                    role = await _Database.Roles.CreateAsync(new UserRole
                    {
                        TenantId = null,
                        Name = def.Name,
                        Description = def.Description,
                        IsBuiltIn = true,
                        IsProtected = def.IsProtected,
                        DefaultScope = def.Scope
                    }, token).ConfigureAwait(false);
                }
                else
                {
                    role.Description = def.Description;
                    role.IsBuiltIn = true;
                    role.IsProtected = def.IsProtected;
                    role.DefaultScope = def.Scope;
                    role.Active = true;
                    await _Database.Roles.UpdateAsync(role, token).ConfigureAwait(false);
                }

                EnumerationResult<RolePermissionMap> maps = await _Database.RolePermissionMaps.EnumerateAsync(new RolePermissionMapFilter { TenantId = null, RoleId = role.Id, PageSize = 1000 }, token).ConfigureAwait(false);
                foreach (RolePermissionMap map in maps.Items)
                {
                    Permission? permission = await _Database.Permissions.ReadByIdAsync(map.PermissionId, token).ConfigureAwait(false);
                    if (permission != null && permission.IsBuiltIn && permission.TenantId == null)
                        await _Database.Permissions.DeleteByIdAsync(permission.Id, token).ConfigureAwait(false);
                }

                int n = 1;
                foreach (BuiltInPermissionDefinition p in def.Permissions)
                {
                    Permission permission = await _Database.Permissions.CreateAsync(new Permission
                    {
                        TenantId = null,
                        Name = "builtin:" + def.Name + ":" + n,
                        ResourceTypes = p.ResourceTypes.Select(t => t.ToString()).ToList(),
                        OperationTypes = p.Operations.Select(o => o.ToString()).ToList(),
                        PermissionType = p.PermissionType,
                        IsBuiltIn = true,
                        IsProtected = true
                    }, token).ConfigureAwait(false);
                    await _Database.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = null, RoleId = role.Id, PermissionId = permission.Id, IsProtected = true }, token).ConfigureAwait(false);
                    n++;
                }
            }
        }

        #endregion
    }
}
