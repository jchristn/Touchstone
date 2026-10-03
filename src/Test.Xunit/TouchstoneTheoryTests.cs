namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Xunit;
    using global::Xunit.Abstractions;
    using Test.Shared;
    using Touchstone.Core;

    /// <summary>
    /// Runs each shared descriptor as a separate xUnit theory row.
    /// </summary>
    public sealed class TouchstoneTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Initialize with the xUnit output helper.
        /// </summary>
        /// <param name="output">Test output helper.</param>
        public TouchstoneTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Non-skipped shared descriptors as theory rows.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in TouchstoneSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Execute a single shared descriptor.
        /// </summary>
        /// <param name="testCase">Descriptor to execute.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("Running: " + testCase.DisplayName);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
