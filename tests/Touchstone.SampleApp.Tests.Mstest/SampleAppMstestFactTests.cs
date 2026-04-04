namespace Touchstone.SampleApp.Tests.Mstest
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Touchstone.Core;
    using Touchstone.MstestAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// MSTest fact-style test host. All descriptors run in a single [TestMethod].
    /// </summary>
    [TestClass]
    public sealed class SampleAppMstestFactTests : TouchstoneMstestBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SampleAppSuites.All; }
        }

        [TestMethod]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
