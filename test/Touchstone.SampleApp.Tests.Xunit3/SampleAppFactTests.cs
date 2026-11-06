namespace Touchstone.SampleApp.Tests.Xunit3
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.SampleApp.Tests.Shared;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    /// <summary>
    /// Runs every sample case, with suite hooks, inside one xUnit v3 fact.
    /// </summary>
    public sealed class SampleAppFactTests : TouchstoneXunit3FactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SampleAppSuites.All; }
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
