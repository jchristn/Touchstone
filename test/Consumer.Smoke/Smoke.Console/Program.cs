namespace Smoke.Console
{
    using System.Threading.Tasks;

    using Smoke.Shared;
    using Touchstone.Cli;

    internal static class Program
    {
        private static Task<int> Main(string[] args)
        {
            return ConsoleRunner.RunMainAsync(new SmokeSuites().Suites, args);
        }
    }
}
