namespace Test.Shared.Xunit3
{
    using System.Collections.Generic;

    using Test.Shared;
    using Test.Shared.Reflection;
    using Touchstone.Core;
    using Touchstone.Xunit3Adapter;

    /// <summary>
    /// The xUnit v3 adapter contract suites.
    /// </summary>
    public static class Xunit3Suites
    {
        /// <summary>
        /// Every xUnit v3 adapter suite. Each access builds fresh descriptors.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new[]
                {
                    AdapterContractSuites.Suite(new Xunit3Probe()),
                    ApiConventions.Suite("ApiXunit3", typeof(TouchstoneXunit3).Assembly),
                };
            }
        }
    }
}
