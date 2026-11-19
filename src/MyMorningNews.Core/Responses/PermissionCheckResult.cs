namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Whether a principal holds one permission, for UI gating.
    /// </summary>
    public class PermissionCheckResult
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        public ResourceTypeEnum ResourceType { get; set; } = ResourceTypeEnum.All;

        /// <summary>
        /// Operation.
        /// </summary>
        public OperationTypeEnum Operation { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Whether permitted.
        /// </summary>
        public bool Permitted { get; set; } = false;
    }
}
