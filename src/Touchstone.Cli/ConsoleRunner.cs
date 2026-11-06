namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Runs suites in a console host with tabular output, optional JSON and HTML reports, and stable exit codes.
    /// Thread safety: all members are safe to call concurrently with different options and writers.
    /// </summary>
    public static class ConsoleRunner
    {
        /// <summary>
        /// Exit code: every selected case passed or was skipped, and no suite hook failed.
        /// </summary>
        public const int ExitSuccess = 0;

        /// <summary>
        /// Exit code: a case failed or was not run, or a suite hook failed.
        /// </summary>
        public const int ExitFailure = 1;

        /// <summary>
        /// Exit code: the run was canceled.
        /// </summary>
        public const int ExitCanceled = 2;

        /// <summary>
        /// Exit code: the command-line arguments were invalid. No test ran.
        /// </summary>
        public const int ExitInvalidArguments = 3;

        /// <summary>
        /// Run suites with explicit options.
        /// </summary>
        /// <param name="suites">Suites to run, in order.</param>
        /// <param name="options">Runner settings. Null uses the defaults.</param>
        /// <param name="cancellationToken">Caller cancellation. A canceled run returns <see cref="ExitCanceled"/>.</param>
        /// <returns><see cref="ExitSuccess"/>, <see cref="ExitFailure"/>, or <see cref="ExitCanceled"/>.</returns>
        /// <exception cref="ArgumentNullException">The suite list is null.</exception>
        /// <exception cref="ArgumentException">The suite list contains null, or the filter contains an invalid value.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        /// <exception cref="System.IO.IOException">A report file cannot be written. The console summary is written first.</exception>
        /// <remarks>An exception thrown by an additional sink propagates unchanged.</remarks>
        public static async Task<int> RunAsync(
            IReadOnlyList<TestSuiteDescriptor> suites,
            ConsoleRunnerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));

            ConsoleRunnerOptions settings = options ?? new ConsoleRunnerOptions();
            ConsoleResultSink console = new ConsoleResultSink(settings);
            List<ITestResultSink> sinks = new List<ITestResultSink> { console };
            sinks.AddRange(settings.AdditionalSinks);
            ITestResultSink sink = sinks.Count == 1 ? console : new CompositeSink(sinks);

            DateTimeOffset started = DateTimeOffset.UtcNow;
            TestRunSummary summary = await TestExecutor.RunAsync(suites, sink, settings.Run, cancellationToken).ConfigureAwait(false);

            ReportOptions report = new ReportOptions { StartedUtc = started, Redact = settings.Redact };
            if (!string.IsNullOrWhiteSpace(settings.RunId)) report.RunId = settings.RunId;

            if (!string.IsNullOrEmpty(settings.JsonResultsPath))
                await JsonReport.WriteAsync(summary, console.AllResults, settings.JsonResultsPath, report, CancellationToken.None).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(settings.HtmlReportPath))
                await HtmlReport.WriteAsync(summary, console.AllResults, settings.HtmlReportPath, report, CancellationToken.None).ConfigureAwait(false);

            return ExitCodeOf(summary);
        }

        /// <summary>
        /// Parse command-line arguments and run suites; intended to be returned from <c>Main</c>. Ctrl+C cancels the run. Invalid arguments write the error and
        /// usage to standard error and return <see cref="ExitInvalidArguments"/>; <c>--help</c> writes usage and returns
        /// <see cref="ExitSuccess"/>. Neither runs tests.
        /// </summary>
        /// <param name="suites">Suites to run, in order.</param>
        /// <param name="args">Command-line arguments. See <see cref="ConsoleRunnerOptions.Usage"/>.</param>
        /// <param name="cancellationToken">Caller cancellation, combined with Ctrl+C.</param>
        /// <returns>One of the <c>Exit</c> constants.</returns>
        /// <exception cref="ArgumentNullException">The suite list or argument array is null.</exception>
        /// <exception cref="ArgumentException">The suite list contains null.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        /// <exception cref="System.IO.IOException">A report file cannot be written. The console summary is written first.</exception>
        public static async Task<int> RunMainAsync(
            IReadOnlyList<TestSuiteDescriptor> suites,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));
            if (args == null) throw new ArgumentNullException(nameof(args));

            ConsoleRunnerOptions options;
            try
            {
                options = ConsoleRunnerOptions.Parse(args);
            }
            catch (ConsoleArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ConsoleRunnerOptions.Usage);
                return ExitInvalidArguments;
            }

            if (options.ShowHelp)
            {
                Console.Out.WriteLine(ConsoleRunnerOptions.Usage);
                return ExitSuccess;
            }

            using (CancellationTokenSource interrupt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                ConsoleCancelEventHandler handler = (sender, e) =>
                {
                    e.Cancel = true;
                    interrupt.Cancel();
                };

                Console.CancelKeyPress += handler;
                try
                {
                    return await RunAsync(suites, options, interrupt.Token).ConfigureAwait(false);
                }
                finally
                {
                    Console.CancelKeyPress -= handler;
                }
            }
        }

        /// <summary>
        /// Map a summary to an exit code.
        /// </summary>
        /// <param name="summary">The run summary.</param>
        /// <returns><see cref="ExitCanceled"/> for a canceled run, <see cref="ExitSuccess"/> for a successful run, and
        /// <see cref="ExitFailure"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">The summary is null.</exception>
        public static int ExitCodeOf(TestRunSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (summary.WasCanceled) return ExitCanceled;
            return summary.IsSuccess ? ExitSuccess : ExitFailure;
        }
    }
}
