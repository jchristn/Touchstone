namespace Touchstone.MstestAdapter
{
    using System;
    using System.Collections.Generic;

    using Touchstone.Core;

    /// <summary>
    /// Provides test case descriptors as MSTest DynamicData rows.
    /// </summary>
    public static class TouchstoneDynamicData
    {
        /// <summary>
        /// Yield all non-skipped test case descriptors as object arrays for DynamicData.
        /// </summary>
        /// <param name="suites">Suites whose cases become DynamicData rows.</param>
        /// <returns>Enumerable of object arrays, each containing one TestCaseDescriptor.</returns>
        public static IEnumerable<object[]> FromSuites(IReadOnlyList<TestSuiteDescriptor> suites)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));

            foreach (TestSuiteDescriptor suite in suites)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        yield return new object[] { testCase };
                }
            }
        }
    }
}
