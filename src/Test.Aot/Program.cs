namespace Test.Aot
{
    using System.Linq;
    using System.Threading.Tasks;

    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Native AOT host for the Touchstone contract suites. Same entry point and options as Test.Automated. The
    /// reflection-based API convention suites (Test.Shared.Reflection) do not run here; README "AOT support" lists them.
    /// </summary>
    internal static class Program
    {
        private static Task<int> Main(string[] args)
        {
            return ConsoleRunner.RunMainAsync(TouchstoneSuites.All.Concat(ExitProbe.Suites()).ToList(), args);
        }
    }
}
