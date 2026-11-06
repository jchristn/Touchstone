namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable outcome of a single test case. The engine creates results; the public constructor exists so custom
    /// sinks and reporters can be tested.
    /// Thread safety: safe to read concurrently.
    /// </summary>
    public sealed class TestResult
    {
        private readonly string _SuiteId;
        private readonly string _CaseId;
        private readonly string _DisplayName;
        private readonly TestOutcome _Outcome;
        private readonly TimeSpan _Duration;
        private readonly string? _Message;
        private readonly Exception? _Exception;
        private readonly IReadOnlyList<string> _Tags;
        private readonly ScenarioMetadata? _Metadata;
        private readonly IReadOnlyList<ScenarioStepResult> _Steps;
        private readonly IReadOnlyList<Exception> _CleanupExceptions;

        /// <summary>
        /// Initialize a new test result.
        /// </summary>
        /// <param name="suiteId">Identifier of the parent suite.</param>
        /// <param name="caseId">Identifier of the case within the suite.</param>
        /// <param name="displayName">Human-readable case name.</param>
        /// <param name="outcome">Final outcome.</param>
        /// <param name="duration">Wall-clock execution time. Zero for cases that did not run. Cannot be negative.</param>
        /// <param name="message">Failure, skip, or not-run reason. Null when there is nothing to report.</param>
        /// <param name="exception">The original exception for a failed or canceled case. Null otherwise.</param>
        /// <param name="tags">The case's tags. Null means none. Copied.</param>
        /// <param name="metadata">Scenario metadata. Null for ordinary cases.</param>
        /// <param name="steps">Scenario step results in order. Null means none. Copied.</param>
        /// <param name="cleanupExceptions">Secondary cleanup failures. Null means none. Copied.</param>
        /// <exception cref="ArgumentNullException">An identity or the display name is null, or a collection contains null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The outcome is undefined or the duration is negative.</exception>
        public TestResult(
            string suiteId,
            string caseId,
            string displayName,
            TestOutcome outcome,
            TimeSpan duration,
            string? message = null,
            Exception? exception = null,
            IReadOnlyList<string>? tags = null,
            ScenarioMetadata? metadata = null,
            IReadOnlyList<ScenarioStepResult>? steps = null,
            IReadOnlyList<Exception>? cleanupExceptions = null)
        {
            _SuiteId = suiteId ?? throw new ArgumentNullException(nameof(suiteId));
            _CaseId = caseId ?? throw new ArgumentNullException(nameof(caseId));
            _DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome.");
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");

            _Outcome = outcome;
            _Duration = duration;
            _Message = message;
            _Exception = exception;
            _Tags = Copy(tags, nameof(tags));
            _Metadata = metadata;
            _Steps = Copy(steps, nameof(steps));
            _CleanupExceptions = Copy(cleanupExceptions, nameof(cleanupExceptions));
        }

        /// <summary>
        /// Composite identity, <c>SuiteId + "." + CaseId</c>.
        /// </summary>
        public string TestId
        {
            get { return _SuiteId + "." + _CaseId; }
        }

        /// <summary>
        /// Identifier of the parent suite. Never null.
        /// </summary>
        public string SuiteId
        {
            get { return _SuiteId; }
        }

        /// <summary>
        /// Identifier of the case within the suite. Never null.
        /// </summary>
        public string CaseId
        {
            get { return _CaseId; }
        }

        /// <summary>
        /// Human-readable case name. Never null.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
        }

        /// <summary>
        /// Final outcome.
        /// </summary>
        public TestOutcome Outcome
        {
            get { return _Outcome; }
        }

        /// <summary>
        /// Wall-clock execution time. Zero for cases that did not run.
        /// </summary>
        public TimeSpan Duration
        {
            get { return _Duration; }
        }

        /// <summary>
        /// Failure, skip, or not-run reason. Null when there is nothing to report. May contain consumer text.
        /// </summary>
        public string? Message
        {
            get { return _Message; }
        }

        /// <summary>
        /// The original exception for a failed or canceled case, with its stack trace. Null otherwise.
        /// </summary>
        public Exception? Exception
        {
            get { return _Exception; }
        }

        /// <summary>
        /// The case's tags. Never null.
        /// </summary>
        public IReadOnlyList<string> Tags
        {
            get { return _Tags; }
        }

        /// <summary>
        /// Scenario metadata. Null for ordinary cases.
        /// </summary>
        public ScenarioMetadata? Metadata
        {
            get { return _Metadata; }
        }

        /// <summary>
        /// Scenario step results in order. Never null; empty for ordinary cases.
        /// </summary>
        public IReadOnlyList<ScenarioStepResult> Steps
        {
            get { return _Steps; }
        }

        /// <summary>
        /// Secondary cleanup failures. Never null. A primary failure, if any, stays in <see cref="Exception"/>.
        /// </summary>
        public IReadOnlyList<Exception> CleanupExceptions
        {
            get { return _CleanupExceptions; }
        }

        /// <summary>
        /// Returns the test identity and outcome.
        /// </summary>
        /// <returns>A short description such as <c>Cart.Add: Passed</c>.</returns>
        public override string ToString()
        {
            return TestId + ": " + _Outcome;
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T>? values, string paramName) where T : class
        {
            if (values == null || values.Count == 0) return Array.Empty<T>();

            T[] copy = new T[values.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentNullException(paramName, "Collection contains null.");

            return Array.AsReadOnly(copy);
        }
    }
}
