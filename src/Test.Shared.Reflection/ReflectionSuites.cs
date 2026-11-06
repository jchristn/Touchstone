namespace Test.Shared.Reflection
{
    using System.Collections.Generic;

    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// API convention suites for Touchstone.Core and Touchstone.Cli. JIT hosts only.
    /// </summary>
    public static class ReflectionSuites
    {
        /// <summary>
        /// The Core and Cli API convention suites. Each access builds fresh descriptors.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new[]
                {
                    ApiConventions.Suite("ApiCore", typeof(TestExecutor).Assembly, typeof(ScenarioBuilder<>)),
                    ApiConventions.Suite("ApiCli", typeof(ConsoleRunner).Assembly),
                };
            }
        }
    }
}
