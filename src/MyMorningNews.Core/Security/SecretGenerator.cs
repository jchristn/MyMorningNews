namespace MyMorningNews.Core.Security
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// Generates access keys, secret keys, and random tokens from a cryptographic RNG.
    /// Thread safety: stateless.
    /// </summary>
    public static class SecretGenerator
    {
        #region Private-Members

        private const string _Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const string _HighEntropy = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.~";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate an access key: access_ plus 32 alphanumeric characters.
        /// </summary>
        /// <returns>Access key.</returns>
        public static string AccessKey()
        {
            return "access_" + RandomString(_Alphanumeric, 32);
        }

        /// <summary>
        /// Generate a secret key: secret_ plus 40 high-entropy characters.
        /// </summary>
        /// <returns>Secret key.</returns>
        public static string SecretKey()
        {
            return "secret_" + RandomString(_HighEntropy, 40);
        }

        /// <summary>
        /// Generate a URL-safe random token.
        /// </summary>
        /// <param name="bytes">Random bytes. Default 32. Minimum 16, maximum 128.</param>
        /// <returns>Base64url token.</returns>
        public static string Token(int bytes = 32)
        {
            byte[] data = RandomNumberGenerator.GetBytes(Math.Clamp(bytes, 16, 128));
            return Base64Url.Encode(data);
        }

        /// <summary>
        /// Random string from an alphabet.
        /// </summary>
        /// <param name="alphabet">Alphabet.</param>
        /// <param name="length">Length. Minimum 1, maximum 1024.</param>
        /// <returns>Random string.</returns>
        /// <exception cref="ArgumentNullException">alphabet is null or empty.</exception>
        public static string RandomString(string alphabet, int length)
        {
            if (String.IsNullOrEmpty(alphabet)) throw new ArgumentNullException(nameof(alphabet));
            int len = Math.Clamp(length, 1, 1024);
            char[] chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            return new String(chars);
        }

        #endregion
    }
}
