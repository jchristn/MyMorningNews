namespace MyMorningNews.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// The REST credentials of the MCP caller: a bearer session token, or an access key and secret key with an optional
    /// tenant. The MCP server holds no privileges of its own; every REST call carries these.
    /// </summary>
    public class CallerCredentials
    {
        #region Public-Members

        /// <summary>
        /// Bearer session token. Null when an access key is used.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Access key. Null when a token is used.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Secret key paired with the access key.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Tenant, sent with access keys. Null to let the server decide.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// True when a token, or an access key and secret key, are present.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                return !String.IsNullOrEmpty(Token) || (!String.IsNullOrEmpty(AccessKey) && !String.IsNullOrEmpty(SecretKey));
            }
        }

        #endregion

        #region Private-Members

        private const string _TokenClaim = "mmn.token";
        private const string _AccessKeyClaim = "mmn.accesskey";
        private const string _SecretKeyClaim = "mmn.secretkey";
        private const string _TenantClaim = "mmn.tenant";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read credentials from authentication claims.
        /// </summary>
        /// <param name="claims">Claims; null gives empty credentials.</param>
        /// <returns>Credentials.</returns>
        public static CallerCredentials FromClaims(IReadOnlyDictionary<string, string>? claims)
        {
            CallerCredentials c = new CallerCredentials();
            if (claims == null) return c;
            c.Token = claims.TryGetValue(_TokenClaim, out string? t) ? t : null;
            c.AccessKey = claims.TryGetValue(_AccessKeyClaim, out string? a) ? a : null;
            c.SecretKey = claims.TryGetValue(_SecretKeyClaim, out string? s) ? s : null;
            c.TenantId = claims.TryGetValue(_TenantClaim, out string? n) ? n : null;
            return c;
        }

        /// <summary>
        /// Write the credentials as authentication claims, kept in memory for the request only.
        /// </summary>
        /// <returns>Claims.</returns>
        public Dictionary<string, string> ToClaims()
        {
            Dictionary<string, string> claims = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Token != null) claims[_TokenClaim] = Token;
            if (AccessKey != null) claims[_AccessKeyClaim] = AccessKey;
            if (SecretKey != null) claims[_SecretKeyClaim] = SecretKey;
            if (TenantId != null) claims[_TenantClaim] = TenantId;
            return claims;
        }

        /// <summary>
        /// A SHA-256 fingerprint of the credential material, used as a cache key so secrets are never stored as keys.
        /// </summary>
        /// <returns>Lowercase hex fingerprint.</returns>
        public string Fingerprint()
        {
            string material = (Token ?? String.Empty) + "\n" + (AccessKey ?? String.Empty) + "\n" + (SecretKey ?? String.Empty) + "\n" + (TenantId ?? String.Empty);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        #endregion
    }
}
