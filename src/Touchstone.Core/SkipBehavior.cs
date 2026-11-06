namespace Touchstone.Core
{
    /// <summary>
    /// How a run treats cases that carry a skip reason.
    /// </summary>
    public enum SkipBehavior
    {
        /// <summary>
        /// Default. Skipped cases do not run and are reported as <see cref="TestOutcome.Skipped"/>.
        /// Framework adapters use the framework's native skip.
        /// </summary>
        Report = 0,

        /// <summary>
        /// Skipped cases are treated like filtered cases: they do not run, produce no result, and are counted only in
        /// <see cref="TestRunSummary.Filtered"/>.
        /// </summary>
        Omit = 1,

        /// <summary>
        /// Skipped cases do not run and are reported as <see cref="TestOutcome.Failed"/> with a
        /// <see cref="SkippedCaseNotAllowedException"/>. Useful in CI to catch forgotten skips.
        /// </summary>
        Fail = 2,
    }
}
