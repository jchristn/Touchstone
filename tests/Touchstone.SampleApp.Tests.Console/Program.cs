namespace Touchstone.SampleApp.Tests.Console
{
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.SampleApp.Tests.Shared;
    using Touchstone.Cli;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            string resultsPath = null;
            string htmlPath = null;
            ConsoleResultSink sink = new ConsoleResultSink();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length) resultsPath = args[++i];
                else if (args[i] == "--html" && i + 1 < args.Length) htmlPath = args[++i];
                else if (args[i] == "--steps") sink.ExpandSteps = true;
                else if (args[i] == "--fail") SampleAppSuites.IncludeFailureToggle = true;
            }
            int code = await ConsoleRunner.RunAsync(SampleAppSuites.All, sink, resultsPath, CancellationToken.None).ConfigureAwait(false);
            if (htmlPath != null) await ScenarioHtmlReport.WriteAsync(sink.AllResults, htmlPath, "console-run", CancellationToken.None, sink.Redact).ConfigureAwait(false);
            return code;
        }
    }
}
