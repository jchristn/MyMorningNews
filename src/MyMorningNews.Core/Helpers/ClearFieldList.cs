namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Requests;

    /// <summary>
    /// Rules for the clearFields list on update requests. An update leaves a property unchanged when it is null or absent, so
    /// clearing an optional value back to null needs its own instruction: the property name in clearFields.
    /// Names are JSON property names and are matched without regard to case. Thread safety: all members are safe for concurrent use.
    /// </summary>
    public static class ClearFieldList
    {
        /// <summary>
        /// Model endpoint properties that can be cleared to null.
        /// </summary>
        public static IReadOnlyList<string> ModelEndpoint { get; } = new List<string>
        {
            "endpoint", "credentialId", "authMethod", "model", "embeddingModel", "temperature", "reasoningEffort", "contextLength",
            "inputCostPerMTok", "outputCostPerMTok", "cachedInputCostPerMTok", "cacheWriteCostPerMTok"
        }.AsReadOnly();

        /// <summary>
        /// Vault credential properties that can be cleared to null.
        /// </summary>
        public static IReadOnlyList<string> VaultCredential { get; } = new List<string>
        {
            "headerName", "username", "description", "expiresUtc"
        }.AsReadOnly();

        /// <summary>
        /// Whether a field is listed for clearing.
        /// </summary>
        /// <param name="requested">The request's clearFields list. May be null.</param>
        /// <param name="field">JSON property name.</param>
        /// <returns>True when the field is listed.</returns>
        /// <exception cref="ArgumentNullException">field is null.</exception>
        public static bool Contains(List<string>? requested, string field)
        {
            ArgumentNullException.ThrowIfNull(field);
            return requested != null && requested.Any(r => r != null && String.Equals(r.Trim(), field, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check a clearFields list against the names that may be cleared and the names the same request gives a value.
        /// </summary>
        /// <param name="requested">The request's clearFields list. May be null or empty.</param>
        /// <param name="allowed">Names that may be cleared.</param>
        /// <param name="supplied">Names the request also gives a value.</param>
        /// <returns>Null when the list is valid; otherwise a message for the caller.</returns>
        /// <exception cref="ArgumentNullException">allowed or supplied is null.</exception>
        public static string? Validate(List<string>? requested, IReadOnlyList<string> allowed, IEnumerable<string> supplied)
        {
            ArgumentNullException.ThrowIfNull(allowed);
            ArgumentNullException.ThrowIfNull(supplied);
            if (requested == null || requested.Count == 0) return null;
            List<string> given = supplied.ToList();
            foreach (string? name in requested)
            {
                string trimmed = (name ?? String.Empty).Trim();
                if (!allowed.Any(a => String.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase)))
                    return "clearFields: '" + trimmed + "' cannot be cleared. Fields that can be cleared: " + String.Join(", ", allowed) + ".";
                if (given.Any(g => String.Equals(g, trimmed, StringComparison.OrdinalIgnoreCase)))
                    return "clearFields: '" + trimmed + "' is also given a value. Send a value or clear the field, not both.";
            }

            return null;
        }

        /// <summary>
        /// Check the clearFields list of a model endpoint request.
        /// </summary>
        /// <param name="req">Request.</param>
        /// <returns>Null when the list is valid; otherwise a message for the caller.</returns>
        /// <exception cref="ArgumentNullException">req is null.</exception>
        public static string? Validate(ModelEndpointUpsertRequest req)
        {
            ArgumentNullException.ThrowIfNull(req);
            List<string> supplied = new List<string>();
            if (!String.IsNullOrWhiteSpace(req.Endpoint)) supplied.Add("endpoint");
            if (!String.IsNullOrWhiteSpace(req.CredentialId) || !String.IsNullOrEmpty(req.ApiKey)) supplied.Add("credentialId");
            if (req.AuthMethod.HasValue) supplied.Add("authMethod");
            if (!String.IsNullOrWhiteSpace(req.Model)) supplied.Add("model");
            if (!String.IsNullOrWhiteSpace(req.EmbeddingModel)) supplied.Add("embeddingModel");
            if (req.Temperature.HasValue) supplied.Add("temperature");
            if (req.ReasoningEffort.HasValue) supplied.Add("reasoningEffort");
            if (req.ContextLength.HasValue) supplied.Add("contextLength");
            if (req.InputCostPerMTok.HasValue) supplied.Add("inputCostPerMTok");
            if (req.OutputCostPerMTok.HasValue) supplied.Add("outputCostPerMTok");
            if (req.CachedInputCostPerMTok.HasValue) supplied.Add("cachedInputCostPerMTok");
            if (req.CacheWriteCostPerMTok.HasValue) supplied.Add("cacheWriteCostPerMTok");
            return Validate(req.ClearFields, ModelEndpoint, supplied);
        }

        /// <summary>
        /// Check the clearFields list of a vault credential request.
        /// </summary>
        /// <param name="req">Request.</param>
        /// <returns>Null when the list is valid; otherwise a message for the caller.</returns>
        /// <exception cref="ArgumentNullException">req is null.</exception>
        public static string? Validate(VaultCredentialUpsertRequest req)
        {
            ArgumentNullException.ThrowIfNull(req);
            List<string> supplied = new List<string>();
            if (!String.IsNullOrWhiteSpace(req.HeaderName)) supplied.Add("headerName");
            if (!String.IsNullOrWhiteSpace(req.Username)) supplied.Add("username");
            if (!String.IsNullOrWhiteSpace(req.Description)) supplied.Add("description");
            if (req.ExpiresUtc.HasValue) supplied.Add("expiresUtc");
            return Validate(req.ClearFields, VaultCredential, supplied);
        }
    }
}
