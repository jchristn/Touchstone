namespace Test.Shared
{
    /// <summary>
    /// Named suite sets that adapter probes turn into native rows.
    /// </summary>
    public enum AdapterFixtureSet
    {
        /// <summary>
        /// AdpA: Pass (tags fast, db), Fail, Skip, Scn (passing scenario); AdpB: Other (tag slow).
        /// </summary>
        Standard = 0,

        /// <summary>
        /// No suites.
        /// </summary>
        Empty = 1,

        /// <summary>
        /// One suite with no cases.
        /// </summary>
        EmptySuites = 2,

        /// <summary>
        /// Two suites with the same identity.
        /// </summary>
        Duplicate = 3,

        /// <summary>
        /// AdpA: Wait, a case that waits for cancellation.
        /// </summary>
        Cancel = 4,
    }
}
