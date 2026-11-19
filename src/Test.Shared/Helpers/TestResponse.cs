namespace Test.Shared.Helpers
{
    using System;
    using MyMorningNews.Core.Serialization;

    /// <summary>
    /// HTTP response captured by <see cref="TestApiClient"/>.
    /// </summary>
    public class TestResponse
    {
        /// <summary>
        /// Status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Body text.
        /// </summary>
        public string Body { get; set; } = String.Empty;

        /// <summary>
        /// Deserialize the body.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>Value.</returns>
        /// <exception cref="InvalidOperationException">The body is empty or not the expected type.</exception>
        public T Json<T>()
        {
            if (String.IsNullOrWhiteSpace(Body)) throw new InvalidOperationException("Expected a JSON body but the response (status " + StatusCode + ") was empty.");
            T? value = Serializer.Deserialize<T>(Body);
            if (value == null) throw new InvalidOperationException("The response body was null.");
            return value;
        }

        /// <summary>
        /// Assert the status code.
        /// </summary>
        /// <param name="expected">Expected status.</param>
        /// <param name="what">Description.</param>
        /// <returns>This response.</returns>
        /// <exception cref="InvalidOperationException">The status differs.</exception>
        public TestResponse Expect(int expected, string what)
        {
            if (StatusCode != expected)
                throw new InvalidOperationException(what + ": expected HTTP " + expected + " but got " + StatusCode + ". Body: " + (Body.Length > 400 ? Body.Substring(0, 400) : Body));
            return this;
        }
    }
}
