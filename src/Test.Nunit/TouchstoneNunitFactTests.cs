namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every shared descriptor inside a single NUnit test.
    /// </summary>
    [TestFixture]
    public sealed class TouchstoneNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
