namespace MyMorningNews.Core.Security
{
    using System;

    /// <summary>
    /// URL-safe Base64 without padding.
    /// </summary>
    public static class Base64Url
    {
        /// <summary>
        /// Encode bytes.
        /// </summary>
        /// <param name="data">Bytes.</param>
        /// <returns>Encoded string.</returns>
        /// <exception cref="ArgumentNullException">data is null.</exception>
        public static string Encode(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Decode a string.
        /// </summary>
        /// <param name="value">Encoded string.</param>
        /// <returns>Bytes, or null when malformed.</returns>
        public static byte[]? Decode(string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            string s = value.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: return null;
            }

            try
            {
                return Convert.FromBase64String(s);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
