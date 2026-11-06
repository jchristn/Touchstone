namespace Touchstone.SampleApp.Tests.Console
{
    using System.Linq;
    using System.Threading.Tasks;

    using Touchstone.Cli;
    using Touchstone.SampleApp.Tests.Shared;

    /// <summary>
    /// Console host for the sample suites. Accepts every ConsoleRunner option, for example <c>--tag smoke</c>, plus
    /// <c>--fail</c>, which adds an intentionally failing case to show failure output.
    /// </summary>
    internal static class Program
    {
        private static Task<int> Main(string[] args)
        {
            SampleAppSuites.IncludeFailureToggle = args.Contains("--fail");
            return ConsoleRunner.RunMainAsync(SampleAppSuites.All, args.Where(a => a != "--fail").ToArray());
        }
    }
}
