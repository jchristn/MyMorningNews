namespace MyMorningNews.Core.Security
{
    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// The signed-request scheme (MMN1-HMAC-SHA256). A client sends x-access-key, x-tenant-guid, x-date (ISO 8601 UTC),
    /// x-nonce (8 to 128 characters, unique per request), and x-signature: the lowercase hex HMAC-SHA256, keyed with the
    /// UTF-8 secret key, of these lines joined by a line feed: the algorithm name, the upper-case method, the path and
    /// query exactly as sent, the tenant, x-date, x-nonce, and the lowercase hex SHA-256 of the body (of nothing when
    /// there is no body). The secret never travels. Thread safe (stateless).
    /// </summary>
    public static class RequestSigner
    {
        /// <summary>
        /// Algorithm name, the first line of the string to sign.
        /// </summary>
        public const string Algorithm = "MMN1-HMAC-SHA256";

        /// <summary>
        /// Build the string to sign.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="pathAndQuery">Path and query as sent, for example /v1.0/api/topics?pageSize=5.</param>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="date">x-date value.</param>
        /// <param name="nonce">x-nonce value.</param>
        /// <param name="bodySha256Hex">Lowercase hex SHA-256 of the body.</param>
        /// <returns>String to sign.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
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
        /// <param name="body">Body bytes; null is treated as empty.</param>
        /// <returns>Hex digest.</returns>
        public static string BodyHash(byte[]? body)
        {
            return Convert.ToHexString(SHA256.HashData(body ?? Array.Empty<byte>())).ToLowerInvariant();
        }

        /// <summary>
        /// Compute the signature.
        /// </summary>
        /// <param name="secretKey">Secret key.</param>
        /// <param name="stringToSign">String to sign.</param>
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
        /// Parse an x-date value (ISO 8601 with a zone, or RFC 1123).
        /// </summary>
        /// <param name="value">Header value.</param>
        /// <param name="utc">Parsed instant in UTC.</param>
        /// <returns>True when parsed.</returns>
        public static bool TryParseDate(string? value, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (String.IsNullOrWhiteSpace(value)) return false;
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
            {
                utc = parsed.UtcDateTime;
                return true;
            }

            return false;
        }
    }
}
