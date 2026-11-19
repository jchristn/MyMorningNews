namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Evaluates expanded grants: explicit deny wins, then permit, then implicit deny.
    /// Thread safety: stateless.
    /// </summary>
    public static class PermissionEvaluator
    {
        /// <summary>
        /// Evaluate grants against a required permission.
        /// </summary>
        /// <param name="grants">Expanded grants.</param>
        /// <param name="resourceType">Required resource type.</param>
        /// <param name="operation">Required operation.</param>
        /// <param name="resourceId">Target resource, when the operation targets one.</param>
        /// <param name="matched">Grants that decided the outcome.</param>
        /// <returns>Outcome.</returns>
        /// <exception cref="ArgumentNullException">grants is null.</exception>
        public static AuthorizationOutcomeEnum Evaluate(
            IEnumerable<EffectiveGrant> grants,
            ResourceTypeEnum resourceType,
            OperationTypeEnum operation,
            string? resourceId,
            out List<EffectiveGrant> matched)
        {
            ArgumentNullException.ThrowIfNull(grants);
            List<EffectiveGrant> applicable = grants
                .Where(g => g.MatchesResource(resourceType) && g.MatchesOperation(operation) && AppliesToScope(g, resourceId))
                .ToList();

            List<EffectiveGrant> denies = applicable.Where(g => g.PermissionType == PermissionTypeEnum.Deny).ToList();
            if (denies.Any())
            {
                matched = denies;
                return AuthorizationOutcomeEnum.DeniedExplicit;
            }

            List<EffectiveGrant> permits = applicable.Where(g => g.PermissionType == PermissionTypeEnum.Permit).ToList();
            if (permits.Any())
            {
                matched = permits;
                return AuthorizationOutcomeEnum.Permitted;
            }

            matched = new List<EffectiveGrant>();
            return AuthorizationOutcomeEnum.DeniedImplicit;
        }

        private static bool AppliesToScope(EffectiveGrant grant, string? resourceId)
        {
            if (grant.ResourceScope == ResourceScopeEnum.Resource)
                return !String.IsNullOrEmpty(resourceId) && String.Equals(grant.ResourceId, resourceId, StringComparison.Ordinal);
            if (!String.IsNullOrEmpty(resourceId)) return grant.InheritsToChildren;
            return true;
        }
    }
}
