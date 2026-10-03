namespace Test.Mstest
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Runs each shared descriptor as a separate MSTest data row.
    /// </summary>
    [TestClass]
    public sealed class TouchstoneMstestTests
    {
        /// <summary>
        /// Non-skipped shared descriptors as DynamicData rows.
        /// </summary>
        /// <returns>Rows, each containing one descriptor.</returns>
        public static IEnumerable<object[]> TestCases()
        {
            return TouchstoneDynamicData.FromSuites(TouchstoneSuites.All);
        }

        /// <summary>
        /// Execute a single shared descriptor.
        /// </summary>
        /// <param name="testCase">Descriptor to execute.</param>
        /// <returns>Task.</returns>
        [TestMethod]
        [DynamicData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
