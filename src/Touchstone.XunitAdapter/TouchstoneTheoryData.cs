namespace Touchstone.XunitAdapter
{
    using System;
    using System.Collections.Generic;

    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// TheoryData source that yields one row per test case descriptor.
    /// </summary>
    public sealed class TouchstoneTheoryData : TheoryData<TestCaseDescriptor>
    {
        /// <summary>
        /// Initialize theory data from a single suite.
        /// </summary>
        /// <param name="suite">Suite whose cases become theory rows.</param>
        public TouchstoneTheoryData(TestSuiteDescriptor suite)
        {
            if (suite == null) throw new ArgumentNullException(nameof(suite));

            foreach (TestCaseDescriptor testCase in suite.Cases)
            {
                Add(testCase);
            }
        }

        /// <summary>
        /// Initialize theory data from multiple suites.
        /// </summary>
        /// <param name="suites">Suites whose cases become theory rows.</param>
        public TouchstoneTheoryData(IEnumerable<TestSuiteDescriptor> suites)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));

            foreach (TestSuiteDescriptor suite in suites)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    Add(testCase);
                }
            }
        }
    }
}
