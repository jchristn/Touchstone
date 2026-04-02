namespace Touchstone.Cli
{
    /// <summary>
    /// Serialization model for a single test result in JSON export.
    /// </summary>
    public sealed class JsonResultEntry
    {
        /// <summary>
        /// Composite test identity.
        /// </summary>
        public string TestId { get; set; } = string.Empty;

        /// <summary>
        /// Parent suite identifier.
        /// </summary>
        public string SuiteId { get; set; } = string.Empty;

        /// <summary>
        /// Test case identifier within the suite.
        /// </summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable test name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// True when the test passed.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// True when the test was skipped.
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Failure or skip message. Null on success.
        /// </summary>
        public string Message { get; set; }
    }
}
