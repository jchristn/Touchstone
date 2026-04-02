using Touchstone.Cli;
using Touchstone.SampleApp.Tests.Shared;

string resultsPath = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--results" && i + 1 < args.Length)
    {
        resultsPath = args[i + 1];
        break;
    }

    if (args[i] == "--fail")
    {
        SampleAppSuites.IncludeFailureToggle = true;
    }
}

return await ConsoleRunner.RunAsync(SampleAppSuites.All, resultsPath: resultsPath);
