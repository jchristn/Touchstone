namespace Touchstone.SampleApp.Tests.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.SampleApp.Tests.Shared;
    using Touchstone.Xunit;
    using global::Xunit;

    /// <summary>
    /// Fact-style test host. All descriptors run in a single [Fact].
    /// </summary>
    public sealed class SampleAppFactTests : TouchstoneFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SampleAppSuites.All; }
        }

        /// <summary>
        /// Run all shared descriptors as a single fact.
        /// </summary>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
