namespace Smoke.Xunit3
{
    using System.Threading.Tasks;

    using Smoke.Shared;
    using Touchstone.Xunit3Adapter;
    using Xunit;

    public sealed class SmokeTests
    {
        public static TheoryData<TouchstoneXunit3Case> Cases()
        {
            return TouchstoneXunit3Cases.From<SmokeSuites>();
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit3Case testCase)
        {
            return TouchstoneXunit3.ExecuteAsync(testCase, TestContext.Current.CancellationToken);
        }
    }
}
