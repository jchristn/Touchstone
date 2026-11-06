namespace Touchstone.Core
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Reported as the exception of a <see cref="TestOutcome.Failed"/> result when a case runs longer than
    /// <see cref="TestRunOptions.CaseTimeout"/>. When the case observed the timeout through its cancellation token,
    /// the exception the case threw is the inner exception.
    /// Thread safety: instances are immutable after construction.
    /// </summary>
    public sealed class CaseTimeoutException : TimeoutException
    {
        /// <summary>
        /// Initialize a new instance with a default message.
        /// </summary>
        public CaseTimeoutException()
            : base("A test case exceeded its timeout.")
        {
            TestId = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance with a message.
        /// </summary>
        /// <param name="message">Error message.</param>
        public CaseTimeoutException(string message)
            : base(message)
        {
            TestId = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance with a message and an inner exception.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Exception that caused this one, or null.</param>
        public CaseTimeoutException(string message, Exception? innerException)
            : base(message, innerException)
        {
            TestId = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance for a specific case and timeout.
        /// </summary>
        /// <param name="testId">Identity of the case that timed out, <c>SuiteId.CaseId</c>.</param>
        /// <param name="timeout">The timeout the case exceeded.</param>
        /// <param name="innerException">The exception the case threw when it observed the timeout, or null.</param>
        public CaseTimeoutException(string testId, TimeSpan timeout, Exception? innerException)
            : base(FormatMessage(testId, timeout), innerException)
        {
            TestId = testId ?? string.Empty;
            Timeout = timeout;
        }

        /// <summary>
        /// Identity of the case that timed out. Empty when not supplied.
        /// </summary>
        public string TestId { get; }

        /// <summary>
        /// The timeout the case exceeded. <see cref="TimeSpan.Zero"/> when not supplied.
        /// </summary>
        public TimeSpan Timeout { get; }

        private static string FormatMessage(string testId, TimeSpan timeout)
        {
            return "Case " + (testId ?? string.Empty) + " exceeded its timeout of "
                + timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s.";
        }
    }
}
