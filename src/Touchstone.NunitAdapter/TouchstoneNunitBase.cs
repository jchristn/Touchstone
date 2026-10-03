namespace Touchstone.NunitAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Base class for NUnit Touchstone tests.
    /// Subclasses call RunAllAsync to execute every descriptor as an individual assertion.
    /// </summary>
    public abstract class TouchstoneNunitBase
    {
        /// <summary>
        /// Suites whose test cases are exercised by this test class.
        /// </summary>
        protected abstract IReadOnlyList<TestSuiteDescriptor> Suites { get; }

        /// <summary>
        /// Execute every non-skipped test case and collect failures.
        /// Throws an AggregateException when any test fails.
        /// Runs through <see cref="TestExecutor.RunAsync"/>, so the run emits Touchstone spans and metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task that completes when all cases have run.</returns>
        protected async Task RunAllAsync(CancellationToken cancellationToken = default)
        {
            TestResultCollector collector = new TestResultCollector();
            await TestExecutor.RunAsync(Suites, collector, cancellationToken).ConfigureAwait(false);

            List<string> failures = new List<string>();
            foreach (TestResult result in collector.Failures)
                failures.Add(result.TestId + ": " + result.Message);

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    failures.Count.ToString() + " test(s) failed: " + string.Join("; ", failures));
            }
        }
    }
}
