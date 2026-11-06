namespace Test.Shared.Mstest
{
    using System.Collections.Generic;

    using Test.Shared;
    using Test.Shared.Reflection;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// The MSTest adapter contract suites.
    /// </summary>
    public static class MstestSuites
    {
        /// <summary>
        /// Every MSTest adapter suite. Each access builds fresh descriptors.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new[]
                {
                    AdapterContractSuites.Suite(new MstestProbe(null)),
                    ApiConventions.Suite("ApiMstest", typeof(TouchstoneMstest).Assembly),
                };
            }
        }
    }
}
