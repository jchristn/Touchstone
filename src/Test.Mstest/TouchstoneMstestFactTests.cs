namespace Test.Mstest
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Runs every shared descriptor inside a single MSTest test method.
    /// </summary>
    [TestClass]
    public sealed class TouchstoneMstestFactTests : TouchstoneMstestBase
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
        [TestMethod]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
