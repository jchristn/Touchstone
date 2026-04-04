namespace Touchstone.MstestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Base class for MSTest Touchstone tests.
    /// Subclasses call RunAllAsync to execute every descriptor as an individual assertion.
    /// </summary>
    public abstract class TouchstoneMstestBase
    {
        /// <summary>
        /// Suites whose test cases are exercised by this test class.
        /// </summary>
        protected abstract IReadOnlyList<TestSuiteDescriptor> Suites { get; }

        /// <summary>
        /// Execute every non-skipped test case and collect failures.
        /// Throws an AggregateException when any test fails.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task that completes when all cases have run.</returns>
        protected async Task RunAllAsync(CancellationToken cancellationToken = default)
        {
            List<string> failures = new List<string>();

            foreach (TestSuiteDescriptor suite in Suites)
            {
                if (suite.BeforeSuiteAsync != null)
                    await suite.BeforeSuiteAsync(cancellationToken);

                try
                {
                    foreach (TestCaseDescriptor testCase in suite.Cases)
                    {
                        if (testCase.Skip)
                            continue;

                        try
                        {
                            await testCase.ExecuteAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            failures.Add(testCase.TestId + ": " + ex.Message);
                        }
                    }
                }
                finally
                {
                    if (suite.AfterSuiteAsync != null)
                        await suite.AfterSuiteAsync(cancellationToken);
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    failures.Count.ToString() + " test(s) failed: " + string.Join("; ", failures));
            }
        }
    }
}
