namespace Touchstone.Core
{
    /// <summary>
    /// Execution outcome of a scenario step.
    /// </summary>
    public enum ScenarioStepOutcome
    {
        /// <summary>
        /// The step was not reached because an earlier operation did not complete successfully.
        /// </summary>
        NotRun = 0,

        /// <summary>
        /// The step completed without an exception.
        /// </summary>
        Passed = 1,

        /// <summary>
        /// The step threw.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// The scenario was skipped, so the step did not run.
        /// </summary>
        Skipped = 3,

        /// <summary>
        /// The step observed caller cancellation.
        /// </summary>
        Canceled = 4,
    }
}
