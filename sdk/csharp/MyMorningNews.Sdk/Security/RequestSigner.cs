namespace MyMorningNews.Sdk.Security
{
    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// The MMN1-HMAC-SHA256 signed-request scheme. A signed request sends x-access-key, x-tenant-guid, x-date (ISO 8601 UTC),
    /// x-nonce (8 to 128 characters, unique per request), and x-signature, and never x-secret-key. The signature is the
    /// lowercase hex HMAC-SHA256, keyed with the UTF-8 secret, of these values joined by a line feed: the algorithm name, the
    /// upper-case method, the path and query exactly as sent, the tenant, x-date, x-nonce, and the lowercase hex SHA-256 of
    /// the body (of nothing when there is no body).
    /// Thread safety: stateless; safe to call from any thread.
    /// </summary>
    public static class RequestSigner
    {
        /// <summary>
        /// Algorithm name, the first line of the string to sign.
        /// </summary>
        public static string Algorithm { get; } = "MMN1-HMAC-SHA256";

        /// <summary>
        /// Build the string to sign.
        /// </summary>
        /// <param name="method">HTTP method (any case).</param>
        /// <param name="pathAndQuery">Path and query exactly as sent, for example /v1.0/api/topics?pageSize=1.</param>
        /// <param name="tenantId">Tenant sent in x-tenant-guid.</param>
        /// <param name="date">x-date value.</param>
        /// <param name="nonce">x-nonce value.</param>
        /// <param name="bodySha256Hex">Hex SHA-256 of the body (see BodyHash).</param>
        /// <returns>The canonical string.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public static string StringToSign(string method, string pathAndQuery, string tenantId, string date, string nonce, string bodySha256Hex)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(pathAndQuery);
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(date);
            ArgumentNullException.ThrowIfNull(nonce);
            ArgumentNullException.ThrowIfNull(bodySha256Hex);
            return Algorithm + "\n" + method.ToUpperInvariant() + "\n" + pathAndQuery + "\n" + tenantId + "\n" + date + "\n" + nonce + "\n" + bodySha256Hex.ToLowerInvariant();
        }

        /// <summary>
        /// Lowercase hex SHA-256 of a body.
        /// </summary>
        /// <param name="body">Body bytes; null hashes the empty body.</param>
        /// <returns>64 lowercase hex characters.</returns>
        public static string BodyHash(byte[]? body)
        {
            return Convert.ToHexString(SHA256.HashData(body ?? Array.Empty<byte>())).ToLowerInvariant();
        }

        /// <summary>
        /// Sign a canonical string.
        /// </summary>
        /// <param name="secretKey">Secret key.</param>
        /// <param name="stringToSign">Canonical string from StringToSign.</param>
        /// <returns>Lowercase hex HMAC-SHA256.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static string Sign(string secretKey, string stringToSign)
        {
            ArgumentNullException.ThrowIfNull(secretKey);
            ArgumentNullException.ThrowIfNull(stringToSign);
            byte[] mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(stringToSign));
            return Convert.ToHexString(mac).ToLowerInvariant();
        }

        /// <summary>
        /// Compute the signature for a request in one call.
        /// </summary>
        /// <param name="secretKey">Secret key.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="pathAndQuery">Path and query exactly as sent.</param>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="date">x-date value.</param>
        /// <param name="nonce">x-nonce value.</param>
        /// <param name="body">Body bytes, or null.</param>
        /// <returns>Lowercase hex signature.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public static string SignRequest(string secretKey, string method, string pathAndQuery, string tenantId, string date, string nonce, byte[]? body)
        {
            return Sign(secretKey, StringToSign(method, pathAndQuery, tenantId, date, nonce, BodyHash(body)));
        }

        /// <summary>
        /// Format a time for x-date (ISO 8601 UTC, whole seconds).
        /// </summary>
        /// <param name="utc">Time; converted to UTC.</param>
        /// <returns>For example 2026-01-01T00:00:00Z.</returns>
        public static string FormatDate(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A random nonce of 32 lowercase hex characters.
        /// </summary>
        /// <returns>Nonce.</returns>
        public static string CreateNonce()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }
    }
}
