namespace MyMorningNews.Core.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// SHA-256 hashing and constant-time comparison for password-equivalent material.
    /// Thread safety: stateless.
    /// </summary>
    public static class SecretHasher
    {
        /// <summary>
        /// SHA-256 of a UTF-8 string, lowercase hex.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>64-character hex hash.</returns>
        /// <exception cref="ArgumentNullException">value is null.</exception>
        public static string Sha256Hex(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        /// <summary>
        /// Constant-time equality of two strings (lengths may leak, contents do not).
        /// </summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        /// <returns>True when equal.</returns>
        public static bool FixedTimeEquals(string? a, string? b)
        {
            if (a == null || b == null) return false;
            byte[] x = Encoding.UTF8.GetBytes(a);
            byte[] y = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(x, y);
        }

        /// <summary>
        /// Verify a password against a stored SHA-256 hex hash. Accepts either the plaintext password or the client-side
        /// SHA-256 hex of it (some clients pre-hash) and compares in constant time.
        /// </summary>
        /// <param name="presented">Presented password or its SHA-256 hex.</param>
        /// <param name="storedSha256">Stored hash.</param>
        /// <returns>True when the password matches.</returns>
        public static bool VerifyPassword(string? presented, string? storedSha256)
        {
            if (String.IsNullOrEmpty(presented) || String.IsNullOrEmpty(storedSha256)) return false;
            string stored = storedSha256.ToLowerInvariant();
            bool plainMatches = FixedTimeEquals(Sha256Hex(presented), stored);
            bool hashMatches = presented.Length == 64 && FixedTimeEquals(presented.ToLowerInvariant(), stored);
            return plainMatches | hashMatches;
        }
    }
}
