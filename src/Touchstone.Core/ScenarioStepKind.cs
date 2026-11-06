namespace Touchstone.Core
{
    /// <summary>
    /// Intent of a behavior step. <c>And</c> steps take the kind of the step before them.
    /// </summary>
    public enum ScenarioStepKind
    {
        /// <summary>
        /// Establishes preconditions.
        /// </summary>
        Given = 0,

        /// <summary>
        /// Performs the action under test.
        /// </summary>
        When = 1,

        /// <summary>
        /// Verifies observable behavior.
        /// </summary>
        Then = 2,
    }
}
