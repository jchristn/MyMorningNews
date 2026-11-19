namespace MyMorningNews.Core.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// AES-256-GCM encryption with a random 96-bit nonce per message (prepended to the ciphertext, then Base64url encoded).
    /// Keys are derived per purpose from the signing key with HMAC-SHA256, so tokens and stored secrets never share a key.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class CryptoProtector
    {
        #region Private-Members

        private const int _NonceBytes = 12;
        private const int _TagBytes = 16;
        private readonly byte[] _Key;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="signingKey">Root key material (any non-empty string).</param>
        /// <param name="purpose">Purpose label, for example session-token or credential-secret.</param>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public CryptoProtector(string signingKey, string purpose)
        {
            if (String.IsNullOrEmpty(signingKey)) throw new ArgumentNullException(nameof(signingKey));
            if (String.IsNullOrEmpty(purpose)) throw new ArgumentNullException(nameof(purpose));
            byte[] root = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
            _Key = HMACSHA256.HashData(root, Encoding.UTF8.GetBytes("mymorningnews:" + purpose));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Encrypt a string.
        /// </summary>
        /// <param name="plaintext">Plaintext.</param>
        /// <returns>Base64url ciphertext.</returns>
        /// <exception cref="ArgumentNullException">plaintext is null.</exception>
        public string Protect(string plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            byte[] output = new byte[_NonceBytes + data.Length + _TagBytes];
            Span<byte> nonce = output.AsSpan(0, _NonceBytes);
            RandomNumberGenerator.Fill(nonce);
            using (AesGcm aes = new AesGcm(_Key, _TagBytes))
            {
                aes.Encrypt(nonce, data, output.AsSpan(_NonceBytes, data.Length), output.AsSpan(_NonceBytes + data.Length, _TagBytes));
            }

            return Base64Url.Encode(output);
        }

        /// <summary>
        /// Decrypt a string produced by <see cref="Protect"/>.
        /// </summary>
        /// <param name="ciphertext">Base64url ciphertext.</param>
        /// <returns>Plaintext, or null when the value is malformed or fails authentication.</returns>
        public string? Unprotect(string? ciphertext)
        {
            if (String.IsNullOrWhiteSpace(ciphertext)) return null;
            byte[]? input = Base64Url.Decode(ciphertext);
            if (input == null || input.Length < _NonceBytes + _TagBytes) return null;
            int length = input.Length - _NonceBytes - _TagBytes;
            byte[] plain = new byte[length];
            try
            {
                using (AesGcm aes = new AesGcm(_Key, _TagBytes))
                {
                    aes.Decrypt(input.AsSpan(0, _NonceBytes), input.AsSpan(_NonceBytes, length), input.AsSpan(_NonceBytes + length, _TagBytes), plain);
                }
            }
            catch (CryptographicException)
            {
                return null;
            }

            return Encoding.UTF8.GetString(plain);
        }

        #endregion
    }
}
