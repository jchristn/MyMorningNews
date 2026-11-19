namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// User as returned by the API (no password material).
    /// </summary>
    public class UserResponse
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Email.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Global administrator.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Protected.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// Preferred time zone.
        /// </summary>
        public string? TimeZone { get; set; } = null;

        /// <summary>
        /// Preferred locale.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// Last login.
        /// </summary>
        public DateTime? LastLoginUtc { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Map from the model.
        /// </summary>
        /// <param name="user">User.</param>
        /// <returns>Response.</returns>
        /// <exception cref="ArgumentNullException">user is null.</exception>
        public static UserResponse FromModel(User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            return new UserResponse
            {
                Id = user.Id, TenantId = user.TenantId, FirstName = user.FirstName, LastName = user.LastName, Email = user.Email,
                IsAdmin = user.IsAdmin, IsTenantAdmin = user.IsTenantAdmin, IsProtected = user.IsProtected, TimeZone = user.TimeZone,
                Locale = user.Locale, LastLoginUtc = user.LastLoginUtc, Active = user.Active, CreatedUtc = user.CreatedUtc, LastUpdateUtc = user.LastUpdateUtc
            };
        }
    }
}
