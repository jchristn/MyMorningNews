namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Security;

    /// <summary>
    /// Offline: the MMN1-HMAC-SHA256 canonical string and signature match a known vector.
    /// </summary>
    public class SignerVectorTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name
        {
            get
            {
                return "Offline: request signer known vector";
            }
        }

        /// <summary>
        /// Offline.
        /// </summary>
        public override bool RequiresServer
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SignerVectorTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            string emptyHash = RequestSigner.BodyHash(null);
            Helpers.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", emptyHash, "empty body hash");
            Helpers.Equal(emptyHash, RequestSigner.BodyHash(new byte[0]), "hash of an empty array");

            string canonical = RequestSigner.StringToSign("get", "/v1.0/api/topics?pageSize=1", "default", "2026-01-01T00:00:00Z", "nonce-12345678", emptyHash.ToUpperInvariant());
            string expectedCanonical = "MMN1-HMAC-SHA256\nGET\n/v1.0/api/topics?pageSize=1\ndefault\n2026-01-01T00:00:00Z\nnonce-12345678\n" + emptyHash;
            Helpers.Equal(expectedCanonical, canonical, "string to sign");

            string signature = RequestSigner.Sign("secret_test", canonical);
            Helpers.Equal("230d17d0e29816b3bec0fd251a730c14a192f0994792062dbebb7512589aae24", signature, "signature");
            Helpers.Equal(signature, RequestSigner.SignRequest("secret_test", "GET", "/v1.0/api/topics?pageSize=1", "default", "2026-01-01T00:00:00Z", "nonce-12345678", null), "one-call signature");

            Helpers.Equal("2026-01-01T00:00:00Z", RequestSigner.FormatDate(new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)), "x-date format");
            string nonce = RequestSigner.CreateNonce();
            Helpers.True(nonce.Length >= 8 && nonce.Length <= 128 && nonce != RequestSigner.CreateNonce(), "nonces are 8 to 128 characters and unique");
            return Task.CompletedTask;
        }
    }
}
