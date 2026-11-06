namespace Parity.Xunit3
{
    using System.Threading.Tasks;

    using Parity.Shared;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    /// <summary>
    /// Parity fixture for xUnit v3.
    /// </summary>
    public sealed class ParityTests
    {
        /// <summary>
        /// Rows.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TouchstoneXunit3Case> Cases()
        {
            return TouchstoneXunit3Cases.From<ParitySuites>(ParitySuites.Options());
        }

        /// <summary>
        /// Run one row.
        /// </summary>
        /// <param name="testCase">Row key.</param>
        /// <returns>The run.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit3Case testCase)
        {
            return TouchstoneXunit3.ExecuteAsync(testCase, ParitySuites.TokenFor(testCase.TestId, TestContext.Current.CancellationToken));
        }
    }
}
