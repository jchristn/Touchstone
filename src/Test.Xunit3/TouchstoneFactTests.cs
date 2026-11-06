namespace Test.Xunit3
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    /// <summary>
    /// Runs every case inside one xUnit v3 fact.
    /// </summary>
    public sealed class TouchstoneFactTests : TouchstoneXunit3FactBase
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
            return RunAllAsync(TestContext.Current.CancellationToken);
        }
    }
}
