namespace Parity.Mstest
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Parity.Shared;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Parity fixture for MSTest.
    /// </summary>
    [TestClass]
    public sealed class ParityTests
    {
        /// <summary>
        /// The running test's context.
        /// </summary>
        public TestContext TestContext { get; set; } = null!;

        /// <summary>
        /// Rows.
        /// </summary>
        /// <returns>Data rows.</returns>
        public static IEnumerable<TestDataRow<TouchstoneMstestCase>> Cases()
        {
            return TouchstoneMstestCases.From<ParitySuites>(ParitySuites.Options());
        }

        /// <summary>
        /// Run one row.
        /// </summary>
        /// <param name="testCase">The case.</param>
        /// <returns>The run.</returns>
        [TestMethod]
        [DynamicData(nameof(Cases))]
        public Task Run(TouchstoneMstestCase testCase)
        {
            return TouchstoneMstest.ExecuteAsync(testCase, TestContext, ParitySuites.TokenFor(testCase.TestId, TestContext.CancellationToken));
        }
    }
}
