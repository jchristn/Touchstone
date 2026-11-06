namespace Parity.Xunit2
{
    using System.Threading;
    using System.Threading.Tasks;

    using Parity.Shared;
    using Touchstone.Xunit2Adapter;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Parity fixture for xUnit v2.
    /// </summary>
    public sealed class ParityTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Initialize with the output helper.
        /// </summary>
        /// <param name="output">Output helper.</param>
        public ParityTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Rows.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TouchstoneXunit2Case> Cases()
        {
            return TouchstoneXunit2Cases.From<ParitySuites>(ParitySuites.Options());
        }

        /// <summary>
        /// Run one row.
        /// </summary>
        /// <param name="testCase">Row key.</param>
        /// <returns>The run.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit2Case testCase)
        {
            return TouchstoneXunit2.ExecuteAsync(testCase, _Output, ParitySuites.TokenFor(testCase.TestId, CancellationToken.None));
        }
    }
}
