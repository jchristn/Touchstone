namespace Test.Automated
{
    using System.Linq;
    using System.Threading.Tasks;

    using Test.Shared;
    using Test.Shared.Reflection;
    using Touchstone.Cli;

    /// <summary>
    /// Console host for the Touchstone contract suites. Accepts every ConsoleRunner option, for example
    /// <c>--results results.json --tag fast</c>.
    /// </summary>
    internal static class Program
    {
        private static Task<int> Main(string[] args)
        {
            return ConsoleRunner.RunMainAsync(TouchstoneSuites.All.Concat(ReflectionSuites.All).Concat(ExitProbe.Suites()).ToList(), args);
        }
    }
}
