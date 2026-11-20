namespace MyMorningNews.Sdk.TestHarness
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Assertions and small utilities for harness cases.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Assert a condition.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="message">What should have held.</param>
        /// <exception cref="HarnessAssertionException">The condition is false.</exception>
        public static void True(bool condition, string message)
        {
            if (!condition) throw new HarnessAssertionException(message);
        }

        /// <summary>
        /// Assert two values are equal.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected.</param>
        /// <param name="actual">Actual.</param>
        /// <param name="what">What is compared.</param>
        /// <exception cref="HarnessAssertionException">The values differ.</exception>
        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new HarnessAssertionException(what + ": expected '" + expected + "' but got '" + actual + "'.");
        }

        /// <summary>
        /// Assert a string is present.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="what">What is checked.</param>
        /// <returns>The value.</returns>
        /// <exception cref="HarnessAssertionException">The value is null or empty.</exception>
        public static string NotEmpty(string? value, string what)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new HarnessAssertionException(what + " should not be empty.");
            return value;
        }

        /// <summary>
        /// Assert a value is not null.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="value">Value.</param>
        /// <param name="what">What is checked.</param>
        /// <returns>The value.</returns>
        /// <exception cref="HarnessAssertionException">The value is null.</exception>
        public static T NotNull<T>(T? value, string what) where T : class
        {
            if (value == null) throw new HarnessAssertionException(what + " should not be null.");
            return value;
        }

        /// <summary>
        /// Assert that an action fails with a typed server error of the given status (and optionally error code).
        /// </summary>
        /// <param name="action">Action expected to fail.</param>
        /// <param name="status">Expected HTTP status.</param>
        /// <param name="errorCode">Expected error code, or null to accept any.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The exception.</returns>
        /// <exception cref="ArgumentNullException">action is null.</exception>
        /// <exception cref="HarnessAssertionException">The action succeeded or failed differently.</exception>
        public static async Task<MyMorningNewsException> ExpectErrorAsync(Func<Task> action, int status, string? errorCode = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            token.ThrowIfCancellationRequested();
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (MyMorningNewsException ex)
            {
                if (ex.StatusCode != status) throw new HarnessAssertionException("Expected HTTP " + status + " but got " + ex.StatusCode + " " + ex.ErrorCode + ": " + ex.Message);
                if (errorCode != null && !String.Equals(ex.ErrorCode, errorCode, StringComparison.Ordinal))
                    throw new HarnessAssertionException("Expected error code " + errorCode + " but got " + ex.ErrorCode + ".");
                return ex;
            }

            throw new HarnessAssertionException("Expected HTTP " + status + (errorCode != null ? " " + errorCode : String.Empty) + " but the call succeeded.");
        }

        /// <summary>
        /// Assert a sequence contains an item matching a predicate.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">Items.</param>
        /// <param name="predicate">Match.</param>
        /// <param name="what">What should be present.</param>
        /// <exception cref="HarnessAssertionException">No item matches.</exception>
        public static void Contains<T>(IEnumerable<T> items, Func<T, bool> predicate, string what)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(predicate);
            if (!items.Any(predicate)) throw new HarnessAssertionException(what + " should be present.");
        }

        /// <summary>
        /// Assert a sequence has no item matching a predicate.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">Items.</param>
        /// <param name="predicate">Match.</param>
        /// <param name="what">What should be absent.</param>
        /// <exception cref="HarnessAssertionException">An item matches.</exception>
        public static void DoesNotContain<T>(IEnumerable<T> items, Func<T, bool> predicate, string what)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(predicate);
            if (items.Any(predicate)) throw new HarnessAssertionException(what + " should not be present.");
        }

        /// <summary>
        /// A unique suffix (UTC time plus random characters).
        /// </summary>
        /// <returns>Suffix.</returns>
        public static string Unique()
        {
            return DateTime.UtcNow.ToString("HHmmss", System.Globalization.CultureInfo.InvariantCulture) + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }
}
