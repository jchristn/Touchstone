namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Serialization model for a single test result in JSON export.
    /// </summary>
    public sealed class JsonResultEntry
    {
        /// <summary>Ordered scenario steps. Empty for ordinary cases.</summary>
        public IReadOnlyList<JsonStepEntry> Steps { get; set; } = Array.Empty<JsonStepEntry>();
        /// <summary>True when matching caller cancellation was observed.</summary>
        public bool Canceled { get; set; }
        /// <summary>Feature description, or null.</summary>
        public string? Feature { get; set; }
        /// <summary>Requirement references.</summary>
        public IReadOnlyList<string> Requirements { get; set; } = Array.Empty<string>();
        /// <summary>Stable example identity, or null.</summary>
        public string? ExampleId { get; set; }
        /// <summary>Cleanup failure messages.</summary>
        public IReadOnlyList<string> CleanupErrors { get; set; } = Array.Empty<string>();
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
        public string? Message { get; set; }
    }
}
