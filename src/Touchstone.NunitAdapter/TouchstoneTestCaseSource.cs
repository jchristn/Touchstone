namespace Touchstone.NunitAdapter
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using Touchstone.Core;

    /// <summary>
    /// Provides test case descriptors as NUnit TestCaseSource data.
    /// </summary>
    public sealed class TouchstoneTestCaseSource : IEnumerable
    {
        private readonly IReadOnlyList<TestSuiteDescriptor> _Suites;

        /// <summary>
        /// Initialize from a list of suites.
        /// </summary>
        /// <param name="suites">Suites whose cases become test case data.</param>
        public TouchstoneTestCaseSource(IReadOnlyList<TestSuiteDescriptor> suites)
        {
            _Suites = suites ?? throw new ArgumentNullException(nameof(suites));
        }

        /// <summary>
        /// Enumerate all non-skipped test case descriptors.
        /// </summary>
        /// <returns>Enumerator of TestCaseDescriptor instances.</returns>
        public IEnumerator GetEnumerator()
        {
            foreach (TestSuiteDescriptor suite in _Suites)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        yield return testCase;
                }
            }
        }
    }
}
