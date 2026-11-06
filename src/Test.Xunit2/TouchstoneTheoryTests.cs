namespace Test.Xunit2
{
    using System.Threading.Tasks;

    using Touchstone.Xunit2Adapter;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Runs each case as its own xUnit v2 theory row.
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
        /// One row per case.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TouchstoneXunit2Case> Cases()
        {
            return TouchstoneXunit2Cases.From<HostSuites>();
        }

        /// <summary>
        /// Execute one case.
        /// </summary>
        /// <param name="testCase">Row key.</param>
        /// <returns>A task that completes when the case passes or is skipped.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit2Case testCase)
        {
            return TouchstoneXunit2.ExecuteAsync(testCase, _Output);
        }
    }
}
