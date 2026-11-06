namespace Touchstone.SampleApp.Tests.Mstest
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Touchstone.MstestAdapter;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// Runs each sample case as its own MSTest data row, with native names, categories, and ignores.
    /// </summary>
    [TestClass]
    public sealed class SampleAppMstestTests
    {
        /// <summary>
        /// The running test's context, set by MSTest.
        /// </summary>
        public TestContext TestContext { get; set; } = null!;

        /// <summary>
        /// One row per case.
        /// </summary>
        /// <returns>Data rows.</returns>
        public static IEnumerable<TestDataRow<TouchstoneMstestCase>> Cases()
        {
            return TouchstoneMstestCases.From<SampleAppSuiteProvider>();
        }

        /// <summary>
        /// Execute one case.
        /// </summary>
        /// <param name="testCase">The case.</param>
        /// <returns>A task that completes when the case passes.</returns>
        [TestMethod]
        [DynamicData(nameof(Cases))]
        public Task Run(TouchstoneMstestCase testCase)
        {
            return TouchstoneMstest.ExecuteAsync(testCase, TestContext, TestContext.CancellationToken);
        }
    }
}
