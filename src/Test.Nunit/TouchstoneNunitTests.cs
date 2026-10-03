namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs each shared descriptor as a separate NUnit test case.
    /// </summary>
    [TestFixture]
    public sealed class TouchstoneNunitTests
    {
        /// <summary>
        /// Execute a single shared descriptor.
        /// </summary>
        /// <param name="testCase">Descriptor to execute.</param>
        /// <returns>Task.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await TestExecutor.ExecuteCaseAsync(testCase, CancellationToken.None).ConfigureAwait(false);
        }

        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(TouchstoneSuites.All);
        }
    }
}
