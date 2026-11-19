namespace Test.Shared.Helpers
{
    using System;

    /// <summary>
    /// Minimal assertions. Failures throw <see cref="InvalidOperationException"/> with context.
    /// </summary>
    public static class Assert
    {
        /// <summary>
        /// Assert a condition is true.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="message">Failure message.</param>
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Assert two values are equal.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <param name="what">Description of the value.</param>
        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!Object.Equals(expected, actual))
                throw new InvalidOperationException(what + ": expected <" + expected + "> but got <" + actual + ">.");
        }

        /// <summary>
        /// Assert a value is not null.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="value">Value.</param>
        /// <param name="what">Description of the value.</param>
        /// <returns>The non-null value.</returns>
        public static T NotNull<T>(T? value, string what) where T : class
        {
            if (value == null) throw new InvalidOperationException(what + " was null.");
            return value;
        }
    }
}
