namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Runner-agnostic assertion helpers. Each failed assertion throws <see cref="TestAssertionException"/>.
    /// </summary>
    public static class TestAssert
    {
        /// <summary>
        /// Assert that a condition is true.
        /// </summary>
        /// <param name="condition">Condition to evaluate.</param>
        /// <param name="message">Message describing the expectation.</param>
        /// <exception cref="TestAssertionException">Thrown when the condition is false.</exception>
        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new TestAssertionException("Expected true: " + message);
        }

        /// <summary>
        /// Assert that a condition is false.
        /// </summary>
        /// <param name="condition">Condition to evaluate.</param>
        /// <param name="message">Message describing the expectation.</param>
        /// <exception cref="TestAssertionException">Thrown when the condition is true.</exception>
        public static void False(bool condition, string message)
        {
            if (condition)
                throw new TestAssertionException("Expected false: " + message);
        }

        /// <summary>
        /// Assert that two values are equal using the default equality comparer.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <param name="context">Description of the value being compared.</param>
        /// <exception cref="TestAssertionException">Thrown when the values differ.</exception>
        public static void Equal<T>(T expected, T actual, string context)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new TestAssertionException(
                    context + ": expected '" + Describe(expected) + "' but got '" + Describe(actual) + "'");
            }
        }

        /// <summary>
        /// Assert that two references point to the same object.
        /// </summary>
        /// <param name="expected">Expected instance.</param>
        /// <param name="actual">Actual instance.</param>
        /// <param name="context">Description of the value being compared.</param>
        /// <exception cref="TestAssertionException">Thrown when the references differ.</exception>
        public static void Same(object? expected, object? actual, string context)
        {
            if (!ReferenceEquals(expected, actual))
                throw new TestAssertionException(context + ": expected the same instance");
        }

        /// <summary>
        /// Assert that a value is null.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <param name="context">Description of the value.</param>
        /// <exception cref="TestAssertionException">Thrown when the value is not null.</exception>
        public static void Null(object? value, string context)
        {
            if (value != null)
                throw new TestAssertionException(context + ": expected null but got '" + Describe(value) + "'");
        }

        /// <summary>
        /// Assert that a value is not null.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <param name="context">Description of the value.</param>
        /// <exception cref="TestAssertionException">Thrown when the value is null.</exception>
        public static void NotNull(object? value, string context)
        {
            if (value == null)
                throw new TestAssertionException(context + ": expected a non-null value");
        }

        /// <summary>
        /// Assert that a string contains a substring (ordinal comparison).
        /// </summary>
        /// <param name="expectedSubstring">Substring that must be present.</param>
        /// <param name="actual">String to search.</param>
        /// <param name="context">Description of the string.</param>
        /// <exception cref="TestAssertionException">Thrown when the substring is absent.</exception>
        public static void Contains(string expectedSubstring, string? actual, string context)
        {
            if (actual == null || !actual.Contains(expectedSubstring, StringComparison.Ordinal))
            {
                throw new TestAssertionException(
                    context + ": expected to contain '" + expectedSubstring + "' but got '" + Describe(actual) + "'");
            }
        }

        /// <summary>
        /// Assert that a string does not contain a substring (ordinal comparison).
        /// </summary>
        /// <param name="unexpectedSubstring">Substring that must be absent.</param>
        /// <param name="actual">String to search.</param>
        /// <param name="context">Description of the string.</param>
        /// <exception cref="TestAssertionException">Thrown when the substring is present.</exception>
        public static void DoesNotContain(string unexpectedSubstring, string? actual, string context)
        {
            if (actual != null && actual.Contains(unexpectedSubstring, StringComparison.Ordinal))
            {
                throw new TestAssertionException(
                    context + ": expected not to contain '" + unexpectedSubstring + "' but got '" + actual + "'");
            }
        }

        /// <summary>
        /// Assert that an action throws exactly the specified exception type.
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <param name="context">Description of the operation.</param>
        /// <returns>The thrown exception.</returns>
        /// <exception cref="TestAssertionException">Thrown when no exception or a different type is thrown.</exception>
        public static TException Throws<TException>(Action action, string context) where TException : Exception
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            try
            {
                action();
            }
            catch (Exception ex)
            {
                return VerifyType<TException>(ex, context);
            }

            throw new TestAssertionException(context + ": expected " + typeof(TException).Name + " but nothing was thrown");
        }

        /// <summary>
        /// Assert that an asynchronous operation throws exactly the specified exception type.
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Operation expected to throw.</param>
        /// <param name="context">Description of the operation.</param>
        /// <returns>The thrown exception.</returns>
        /// <exception cref="TestAssertionException">Thrown when no exception or a different type is thrown.</exception>
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string context) where TException : Exception
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return VerifyType<TException>(ex, context);
            }

            throw new TestAssertionException(context + ": expected " + typeof(TException).Name + " but nothing was thrown");
        }

        private static TException VerifyType<TException>(Exception ex, string context) where TException : Exception
        {
            if (ex.GetType() != typeof(TException))
            {
                throw new TestAssertionException(
                    context + ": expected " + typeof(TException).Name + " but got " + ex.GetType().Name + " (" + ex.Message + ")");
            }

            return (TException)ex;
        }

        private static string Describe(object? value)
        {
            return value == null ? "<null>" : value.ToString() ?? "<null>";
        }
    }
}
