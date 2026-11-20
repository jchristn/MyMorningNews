namespace MyMorningNews.Sdk.Resources
{
    using System;
    using MyMorningNews.Sdk.Resources.Admin;

    /// <summary>
    /// Administration: the control plane (tenants, users, credentials, roles, permissions, assignments), promotional codes,
    /// plans, plan assignments, audit, request history, and settings. Most calls need a tenant or global administrator.
    /// </summary>
    public class AdminResource : ResourceBase
    {
        /// <summary>
        /// Tenants (control plane).
        /// </summary>
        public AdminTenantsResource Tenants { get; }

        /// <summary>
        /// Users within a tenant.
        /// </summary>
        public AdminUsersResource Users { get; }

        /// <summary>
        /// Access key credentials within a tenant.
        /// </summary>
        public AdminCredentialsResource Credentials { get; }

        /// <summary>
        /// Roles.
        /// </summary>
        public AdminRolesResource Roles { get; }

        /// <summary>
        /// Permissions.
        /// </summary>
        public AdminPermissionsResource Permissions { get; }

        /// <summary>
        /// User role assignments and credential scope assignments.
        /// </summary>
        public AdminAssignmentsResource Assignments { get; }

        /// <summary>
        /// Promotional codes.
        /// </summary>
        public AdminPromoCodesResource PromoCodes { get; }

        /// <summary>
        /// Plans.
        /// </summary>
        public AdminPlansResource Plans { get; }

        /// <summary>
        /// Plan assignments.
        /// </summary>
        public AdminPlanAssignmentsResource PlanAssignments { get; }

        /// <summary>
        /// Audit records.
        /// </summary>
        public AdminAuditResource Audit { get; }

        /// <summary>
        /// Captured request history.
        /// </summary>
        public AdminRequestHistoryResource RequestHistory { get; }

        /// <summary>
        /// Server settings.
        /// </summary>
        public AdminSettingsResource Settings { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminResource(MyMorningNewsClient client) : base(client)
        {
            Tenants = new AdminTenantsResource(client);
            Users = new AdminUsersResource(client);
            Credentials = new AdminCredentialsResource(client);
            Roles = new AdminRolesResource(client);
            Permissions = new AdminPermissionsResource(client);
            Assignments = new AdminAssignmentsResource(client);
            PromoCodes = new AdminPromoCodesResource(client);
            Plans = new AdminPlansResource(client);
            PlanAssignments = new AdminPlanAssignmentsResource(client);
            Audit = new AdminAuditResource(client);
            RequestHistory = new AdminRequestHistoryResource(client);
            Settings = new AdminSettingsResource(client);
        }
    }
}
