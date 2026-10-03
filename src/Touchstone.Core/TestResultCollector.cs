namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Result sink that keeps every completed result in memory, in completion order.
    /// Not thread-safe: use one instance per run.
    /// </summary>
    public sealed class TestResultCollector : ITestResultSink
    {
        private readonly List<TestResult> _Results = new List<TestResult>();

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
            get { return _Results; }
        }

        /// <summary>
        /// Results that failed (not successful and not skipped), in completion order. Never null.
        /// </summary>
        public IReadOnlyList<TestResult> Failures
        {
            get { return _Results.FindAll(r => !r.Success && !r.Skipped); }
        }

        /// <inheritdoc />
        public ValueTask OnSuiteStartedAsync(TestSuiteDescriptor suite, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
        public ValueTask OnTestCompletedAsync(TestResult result, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            _Results.Add(result);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask OnSuiteCompletedAsync(TestSuiteDescriptor suite, TestRunSummary summary, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
