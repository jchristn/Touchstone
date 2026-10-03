namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Central source of truth for every Touchstone test suite. All runners consume <see cref="All"/>.
    /// </summary>
    public static class TouchstoneSuites
    {
        /// <summary>
        /// Every test suite, in execution order. A new list of freshly built descriptors is returned on each access.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    DescriptorSuites.TestCaseDescriptorSuite(),
                    DescriptorSuites.TestSuiteDescriptorSuite(),
                    ResultModelSuites.TestResultSuite(),
                    ResultModelSuites.TestRunSummarySuite(),
                    ResultModelSuites.JsonResultEntrySuite(),
                    ExecutorSuites.TestExecutorSuite(),
                    CliSuites.ConsoleResultSinkSuite(),
                    CliSuites.ConsoleRunnerSuite(),
                    AdapterSuites.XunitDataSuite(),
                    AdapterSuites.NunitSourceSuite(),
                    AdapterSuites.MstestDataSuite(),
                    AdapterSuites.XunitFactBaseSuite(),
                    AdapterSuites.NunitBaseSuite(),
                    AdapterSuites.MstestBaseSuite(),
                    TelemetrySuites.TelemetrySuite(),
                };
            }
        }
    }
}
