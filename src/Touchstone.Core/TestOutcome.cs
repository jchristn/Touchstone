namespace Touchstone.Core
{
    /// <summary>
    /// Final outcome of a test case.
    /// </summary>
    public enum TestOutcome
    {
        /// <summary>
        /// The case ran and completed without an exception.
        /// </summary>
        Passed = 0,

        /// <summary>
        /// The case ran and threw, its suite setup failed, its cleanup failed, or it was skipped under <see cref="SkipBehavior.Fail"/>.
        /// </summary>
        Failed = 1,

        /// <summary>
        /// The case was skipped and did not run.
        /// </summary>
        Skipped = 2,

        /// <summary>
        /// The case observed caller cancellation while it ran.
        /// </summary>
        Canceled = 3,

        /// <summary>
        /// The case was selected but never started, because the run was canceled or stopped first.
        /// </summary>
        NotRun = 4,
    }
}
