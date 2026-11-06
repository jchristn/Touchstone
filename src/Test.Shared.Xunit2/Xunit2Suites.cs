namespace Test.Shared.Xunit2
{
    using System.Collections.Generic;

    using Test.Shared;
    using Test.Shared.Reflection;
    using Touchstone.Core;
    using Touchstone.Xunit2Adapter;

    /// <summary>
    /// The xUnit v2 adapter contract suites.
    /// </summary>
    public static class Xunit2Suites
    {
        /// <summary>
        /// Every xUnit v2 adapter suite. Each access builds fresh descriptors.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new[]
                {
                    AdapterContractSuites.Suite(new Xunit2Probe()),
                    ApiConventions.Suite("ApiXunit2", typeof(TouchstoneXunit2).Assembly),
                };
            }
        }
    }
}
