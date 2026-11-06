namespace Touchstone.SampleApp.Tests.Xunit3
{
    using System.Threading.Tasks;

    using Touchstone.SampleApp.Tests.Shared;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    /// <summary>
    /// Runs each sample case as its own xUnit v3 theory row, with native skips, names, and traits.
    /// </summary>
    public sealed class SampleAppTheoryTests
    {
        /// <summary>
        /// One row per case.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TouchstoneXunit3Case> Cases()
        {
            return TouchstoneXunit3Cases.From<SampleAppSuiteProvider>();
        }

        /// <summary>
        /// Execute one case.
        /// </summary>
        /// <param name="testCase">Row key.</param>
        /// <returns>A task that completes when the case passes.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit3Case testCase)
        {
            return TouchstoneXunit3.ExecuteAsync(testCase, TestContext.Current.CancellationToken);
        }
    }
}
