#nullable enable
namespace Touchstone.Core
{
    /// <summary>Intent of a behavior step.</summary>
    public enum ScenarioStepKind
    {
        /// <summary>Establish preconditions.</summary>
        Given,
        /// <summary>Perform the action under test.</summary>
        When,
        /// <summary>Verify observable behavior.</summary>
        Then
    }
}
