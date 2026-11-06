namespace Smoke.Xunit2
{
    using System.Threading.Tasks;

    using Smoke.Shared;
    using Touchstone.Xunit2Adapter;
    using Xunit;
    using Xunit.Abstractions;

    public sealed class SmokeTests
    {
        private readonly ITestOutputHelper _Output;

        public SmokeTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        public static TheoryData<TouchstoneXunit2Case> Cases()
        {
            return TouchstoneXunit2Cases.From<SmokeSuites>();
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Run(TouchstoneXunit2Case testCase)
        {
            return TouchstoneXunit2.ExecuteAsync(testCase, _Output);
        }
    }
}
