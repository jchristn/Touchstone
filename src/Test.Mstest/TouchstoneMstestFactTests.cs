namespace Test.Mstest
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Runs every case inside one MSTest test.
    /// </summary>
    [TestClass]
    public sealed class TouchstoneMstestFactTests : TouchstoneMstestFactBase
    {
        /// <summary>
        /// The running test's context, set by MSTest.
        /// </summary>
        public TestContext TestContext { get; set; } = null!;

        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return new HostSuites().Suites; }
        }

        /// <summary>
        /// Run every suite.
        /// </summary>
        /// <returns>A task that completes when every case passed.</returns>
        [TestMethod]
        public Task RunAll()
        {
            return RunAllAsync(TestContext.CancellationToken);
        }
    }
}
