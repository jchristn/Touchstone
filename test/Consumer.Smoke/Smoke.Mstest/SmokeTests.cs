namespace Smoke.Mstest
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Smoke.Shared;
    using Touchstone.MstestAdapter;

    [TestClass]
    public sealed class SmokeTests
    {
        public TestContext TestContext { get; set; } = null!;

        public static IEnumerable<TestDataRow<TouchstoneMstestCase>> Cases()
        {
            return TouchstoneMstestCases.From<SmokeSuites>();
        }

        [TestMethod]
        [DynamicData(nameof(Cases))]
        public Task Run(TouchstoneMstestCase testCase)
        {
            return TouchstoneMstest.ExecuteAsync(testCase, TestContext, TestContext.CancellationToken);
        }
    }
}
