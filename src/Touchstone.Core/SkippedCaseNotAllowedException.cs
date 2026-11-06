namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Reported as the failure of a skipped case when the run uses <see cref="SkipBehavior.Fail"/>.
    /// Thread safety: instances are immutable after construction.
    /// </summary>
    public sealed class SkippedCaseNotAllowedException : Exception
    {
        /// <summary>
        /// Initialize a new instance with a default message.
        /// </summary>
        public SkippedCaseNotAllowedException()
            : base("Skipped cases are not allowed in this run.")
        {
            TestId = string.Empty;
            SkipReason = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance with a message.
        /// </summary>
        /// <param name="message">Error message.</param>
        public SkippedCaseNotAllowedException(string message)
            : base(message)
        {
            TestId = string.Empty;
            SkipReason = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance with a message and an inner exception.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Exception that caused this one, or null.</param>
        public SkippedCaseNotAllowedException(string message, Exception? innerException)
            : base(message, innerException)
        {
            TestId = string.Empty;
            SkipReason = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance for a specific skipped case.
        /// </summary>
        /// <param name="testId">Identity of the skipped case.</param>
        /// <param name="skipReason">The case's skip reason.</param>
        public SkippedCaseNotAllowedException(string testId, string skipReason)
            : base("Skipped case " + testId + " is not allowed in this run. Skip reason: " + skipReason)
        {
            TestId = testId ?? string.Empty;
            SkipReason = skipReason ?? string.Empty;
        }

        /// <summary>
        /// Identity of the skipped case. Empty when not supplied.
        /// </summary>
        public string TestId { get; }

        /// <summary>
        /// The case's skip reason. Empty when not supplied.
        /// </summary>
        public string SkipReason { get; }
    }
}
