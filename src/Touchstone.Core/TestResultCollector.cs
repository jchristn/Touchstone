namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Result sink that keeps every case result in memory, in completion order.
    /// Thread safety: not thread-safe. Use one instance per run.
    /// </summary>
    public sealed class TestResultCollector : ITestResultSink
    {
        private readonly List<TestResult> _Results = new List<TestResult>();
        private IReadOnlyList<TestResult>? _Failures = null;
        private TestRunSummary? _Summary = null;

        /// <summary>
        /// Initialize a new, empty collector.
        /// </summary>
        public TestResultCollector()
        {
        }

        /// <summary>
        /// Every result received, in completion order. Never null.
        /// </summary>
        public IReadOnlyList<TestResult> Results
        {
            get { return _Results.AsReadOnly(); }
        }

        /// <summary>
        /// Results whose outcome is <see cref="TestOutcome.Failed"/> or <see cref="TestOutcome.Canceled"/>, in completion
        /// order. Never null. Computed once per change, not on every access.
        /// </summary>
        public IReadOnlyList<TestResult> Failures
        {
            get
            {
                if (_Failures == null)
                {
                    List<TestResult> failures = _Results.FindAll(r => r.Outcome == TestOutcome.Failed || r.Outcome == TestOutcome.Canceled);
                    _Failures = failures.AsReadOnly();
                }

                return _Failures;
            }
        }

        /// <summary>
        /// The run summary. Null until a multi-suite run completes.
        /// </summary>
        public TestRunSummary? Summary
        {
            get { return _Summary; }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">The result is null.</exception>
        public ValueTask OnCaseCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            _Results.Add(result);
            _Failures = null;
            return default;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">The summary is null.</exception>
        public ValueTask OnRunCompletedAsync(TestRunSummary summary, CancellationToken cancellationToken)
        {
            _Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            return default;
        }
    }
}
