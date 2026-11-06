namespace Test.Xunit3
{
    using System.Collections.Generic;
    using System.Linq;

    using Test.Shared;
    using Test.Shared.Reflection;
    using Test.Shared.Xunit3;
    using Touchstone.Core;

    /// <summary>
    /// Every suite this host runs: the shared contract suites and the xUnit v3 adapter suites.
    /// </summary>
    public sealed class HostSuites : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TouchstoneSuites.All.Concat(ReflectionSuites.All).Concat(Xunit3Suites.All).ToList(); }
        }
    }
}
