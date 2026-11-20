namespace MyMorningNews.Benchmark.Metrics
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Stable hashes for anything derived from strings (string.GetHashCode is randomized per process): FNV-1a for
    /// feature hashing and seeds, SHA-256 for dataset and configuration fingerprints.
    /// </summary>
    public static class StableHash
    {
        #region Public-Methods

        /// <summary>
        /// 32-bit FNV-1a over the UTF-8 bytes of a string.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Hash.</returns>
        public static uint Fnv1a(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            uint hash = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 16777619;
            }

            return hash;
        }

        /// <summary>
        /// Lowercase hex SHA-256 of a string.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Hash.</returns>
        public static string Sha256(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        /// <summary>
        /// Lowercase hex SHA-256 of a file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Hash.</returns>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        public static string Sha256File(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            using (FileStream fs = File.OpenRead(path))
            {
                return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
            }
        }

        #endregion
    }
}
