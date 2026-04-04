namespace Touchstone.SampleApp.Tests.Mstest
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Touchstone.Core;
    using Touchstone.MstestAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// MSTest test host using DynamicData for data-driven execution.
    /// </summary>
    [TestClass]
    public sealed class SampleAppMstestTests
    {
        /// <summary>
        /// Provides non-skipped test case descriptors as DynamicData rows.
        /// </summary>
        /// <returns>Enumerable of object arrays.</returns>
        public static IEnumerable<object[]> TestCases()
        {
            return TouchstoneDynamicData.FromSuites(SampleAppSuites.All);
        }

        [TestMethod]
        [DynamicData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
