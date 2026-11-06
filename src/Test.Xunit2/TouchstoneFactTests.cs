namespace Test.Xunit2
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.Xunit2Adapter;
    using Xunit;

    /// <summary>
    /// Runs every case inside one xUnit v2 fact.
    /// </summary>
    public sealed class TouchstoneFactTests : TouchstoneXunit2FactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return new HostSuites().Suites; }
        }

        /// <summary>
        /// Run every suite.
        /// </summary>
        /// <returns>A task that completes when every case passed.</returns>
        [Fact]
        public Task RunAll()
        {
            return RunAllAsync();
        }
    }
}
