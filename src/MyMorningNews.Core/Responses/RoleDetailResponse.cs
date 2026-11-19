namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A role with its mapped permissions.
    /// </summary>
    public class RoleDetailResponse
    {
        /// <summary>
        /// Role.
        /// </summary>
        public UserRole Role { get; set; } = new UserRole { Name = "role" };

        /// <summary>
        /// Mapped permissions.
        /// </summary>
        public List<Permission> Permissions { get; set; } = new List<Permission>();
    }
}
