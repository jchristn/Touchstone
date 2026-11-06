namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Linq;

    using Touchstone.Core;

    /// <summary>
    /// Every runner-agnostic Touchstone contract suite. Every host, including Test.Aot, consumes <see cref="All"/>.
    /// JIT hosts also run the reflection-based API convention suites from Test.Shared.Reflection.
    /// Case identities are catalog IDs from archive/V1.0_PLAN.md section 11.
    /// </summary>
    public static class TouchstoneSuites
    {
        /// <summary>
        /// Every suite, in execution order. Each access builds fresh descriptors.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return DescriptorSuites.All()
                    .Concat(ModelSuites.All())
                    .Concat(FilterSuites.All())
                    .Concat(ExecutionSuites.All())
                    .Concat(CancellationSuites.All())
                    .Concat(ScenarioSuites.All())
                    .Concat(SinkSuites.All())
                    .Concat(CliSuites.All())
                    .Concat(ReportSuites.All())
                    .Concat(TelemetrySuites.All())
                    .ToList();
            }
        }
    }
}
