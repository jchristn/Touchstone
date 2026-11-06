namespace Test.Shared.Nunit
{
    using System.Collections.Generic;

    using Test.Shared;
    using Test.Shared.Reflection;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// The NUnit adapter contract suites.
    /// </summary>
    public static class NunitSuites
    {
        /// <summary>
        /// Every NUnit adapter suite. Each access builds fresh descriptors.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new[]
                {
                    AdapterContractSuites.Suite(new NunitProbe()),
                    ApiConventions.Suite("ApiNunit", typeof(TouchstoneNunit).Assembly),
                };
            }
        }
    }
}
