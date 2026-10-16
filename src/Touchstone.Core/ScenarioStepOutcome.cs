#nullable enable
namespace Touchstone.Core
{
    /// <summary>Execution outcome of a scenario step.</summary>
    public enum ScenarioStepOutcome
    {
        /// <summary>The step was not reached.</summary>
        NotRun,
        /// <summary>The step passed.</summary>
        Passed,
        /// <summary>The step failed.</summary>
        Failed,
        /// <summary>The descriptor was skipped.</summary>
        Skipped,
        /// <summary>The caller canceled execution.</summary>
        Canceled
    }
}
