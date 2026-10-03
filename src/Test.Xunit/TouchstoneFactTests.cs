namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::Xunit;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;

    /// <summary>
    /// Runs every shared descriptor inside a single xUnit fact.
    /// </summary>
    public sealed class TouchstoneFactTests : TouchstoneFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TouchstoneSuites.All; }
        }

        /// <summary>
        /// Run all shared descriptors.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
