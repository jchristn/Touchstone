namespace Test.Mstest
{
    using System.Collections.Generic;
    using System.Linq;

    using Test.Shared;
    using Test.Shared.Mstest;
    using Test.Shared.Reflection;
    using Touchstone.Core;

    /// <summary>
    /// Every suite this host runs: the shared contract suites and the MSTest adapter suites.
    /// </summary>
    public sealed class HostSuites : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TouchstoneSuites.All.Concat(ReflectionSuites.All).Concat(MstestSuites.All).ToList(); }
        }
    }
}
