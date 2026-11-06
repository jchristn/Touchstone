namespace Touchstone.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using Touchstone.Core;

    /// <summary>
    /// Settings for <see cref="ConsoleRunner"/>. Build one directly or parse command-line arguments with <see cref="Parse"/>.
    /// Thread safety: not thread-safe while being configured. Do not change an instance while a run is using it.
    /// </summary>
    public sealed class ConsoleRunnerOptions
    {
        /// <summary>
        /// Usage text for the arguments <see cref="Parse"/> accepts.
        /// </summary>
        public static string Usage { get; } =
            "Options:\n"
            + "  --tag <tags>          Run only cases with any of these tags (repeatable, comma-separated).\n"
            + "  --exclude-tag <tags>  Skip cases with any of these tags (repeatable, comma-separated).\n"
            + "  --suite <ids>         Run only these suites (repeatable, comma-separated).\n"
            + "  --test <ids>          Run only these test identities, SuiteId.CaseId (repeatable, comma-separated).\n"
            + "  --skip <mode>         How to treat skipped cases: report (default), omit, or fail.\n"
            + "  --stop-on-failure     Stop the run at the first failed case.\n"
            + "  --case-timeout <s>    Fail any case that runs longer than this many seconds (0.001 to 86400).\n"
            + "  --parallel <n>        Run up to n cases of a suite at the same time (1 to 1024). Default 1.\n"
            + "  --results <path>      Write JSON results to this file.\n"
            + "  --html <path>         Write an HTML report to this file.\n"
            + "  --expand              Show scenario steps as they run.\n"
            + "  --no-color            Disable colored output.\n"
            + "  --help                Show this help.\n"
            + "Exit codes: 0 success, 1 failures or suite hook errors, 2 canceled, 3 invalid arguments.";

        private TestRunOptions _Run = new TestRunOptions();
        private Func<string, string> _Redact = text => text;
        private readonly List<ITestResultSink> _AdditionalSinks = new List<ITestResultSink>();

        /// <summary>
        /// Initialize options with the defaults.
        /// </summary>
        public ConsoleRunnerOptions()
        {
        }

        /// <summary>
        /// Engine settings: filter, skip behavior, stop on first failure, and cleanup timeout. Cannot be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        public TestRunOptions Run
        {
            get { return _Run; }
            set { _Run = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Show scenario steps live as they run. Default false.
        /// </summary>
        public bool ExpandSteps { get; set; }

        /// <summary>
        /// Color output. Null (the default) colors only when writing to <see cref="Console.Out"/> and output is not
        /// redirected. True forces ANSI color sequences into the writer; false disables color.
        /// </summary>
        public bool? UseColor { get; set; }

        /// <summary>
        /// Destination for console output. Null (the default) means <see cref="Console.Out"/>.
        /// </summary>
        public TextWriter? Writer { get; set; }

        /// <summary>
        /// Transforms every consumer-supplied string before it is written to the console or a report.
        /// Default: identity. Cannot be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        public Func<string, string> Redact
        {
            get { return _Redact; }
            set { _Redact = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// JSON results file. Null or empty (the default) writes no file.
        /// </summary>
        public string? JsonResultsPath { get; set; }

        /// <summary>
        /// HTML report file. Null or empty (the default) writes no file.
        /// </summary>
        public string? HtmlReportPath { get; set; }

        /// <summary>
        /// Run name shown in the HTML report. Null means <c>touchstone</c>.
        /// </summary>
        public string? RunId { get; set; }

        /// <summary>
        /// Extra sinks that receive every event alongside the console sink, in order. Never null.
        /// </summary>
        public IList<ITestResultSink> AdditionalSinks
        {
            get { return _AdditionalSinks; }
        }

        /// <summary>
        /// True when <see cref="Parse"/> saw <c>--help</c>.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Parse command-line arguments. See <see cref="Usage"/>. Option names are case-sensitive; values for
        /// <c>--skip</c> are not.
        /// </summary>
        /// <param name="args">Arguments, usually from <c>Main</c>.</param>
        /// <returns>New options.</returns>
        /// <exception cref="ArgumentNullException">The argument array is null.</exception>
        /// <exception cref="ConsoleArgumentException">An option is unknown, is missing its value, or has an invalid value.</exception>
        public static ConsoleRunnerOptions Parse(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            ConsoleRunnerOptions options = new ConsoleRunnerOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? throw new ConsoleArgumentException("Argument " + i + " is null.");
                switch (arg)
                {
                    case "--tag":
                        AddList(options.Run.Filter.IncludeTags, arg, Value(args, ref i, arg));
                        break;
                    case "--exclude-tag":
                        AddList(options.Run.Filter.ExcludeTags, arg, Value(args, ref i, arg));
                        break;
                    case "--suite":
                        AddList(options.Run.Filter.SuiteIds, arg, Value(args, ref i, arg));
                        break;
                    case "--test":
                        AddList(options.Run.Filter.TestIds, arg, Value(args, ref i, arg));
                        break;
                    case "--skip":
                        options.Run.SkipBehavior = ParseSkip(Value(args, ref i, arg));
                        break;
                    case "--stop-on-failure":
                        options.Run.StopOnFirstFailure = true;
                        break;
                    case "--case-timeout":
                        options.Run.CaseTimeout = ParseCaseTimeout(Value(args, ref i, arg));
                        break;
                    case "--parallel":
                        options.Run.MaxDegreeOfParallelism = ParseParallel(Value(args, ref i, arg));
                        break;
                    case "--results":
                        options.JsonResultsPath = Value(args, ref i, arg);
                        break;
                    case "--html":
                        options.HtmlReportPath = Value(args, ref i, arg);
                        break;
                    case "--expand":
                        options.ExpandSteps = true;
                        break;
                    case "--no-color":
                        options.UseColor = false;
                        break;
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    default:
                        throw new ConsoleArgumentException("Unknown option '" + arg + "'.");
                }
            }

            try
            {
                options.Run.Filter.Validate();
            }
            catch (ArgumentException ex)
            {
                throw new ConsoleArgumentException(ex.Message, ex);
            }

            return options;
        }

        private static string Value(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length || args[index + 1] == null || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ConsoleArgumentException("Option '" + option + "' requires a value.");

            index++;
            return args[index];
        }

        private static void AddList(IList<string> target, string option, string value)
        {
            foreach (string part in value.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0) throw new ConsoleArgumentException("Option '" + option + "' has an empty value.");
                target.Add(trimmed);
            }
        }

        private static TimeSpan ParseCaseTimeout(string value)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                || double.IsNaN(seconds) || seconds < 0.001 || seconds > 86400)
                throw new ConsoleArgumentException("Option '--case-timeout' must be a number of seconds from 0.001 to 86400, not '" + value + "'.");
            return TimeSpan.FromSeconds(seconds);
        }

        private static int ParseParallel(string value)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 1 || count > 1024)
                throw new ConsoleArgumentException("Option '--parallel' must be a whole number from 1 to 1024, not '" + value + "'.");
            return count;
        }

        private static SkipBehavior ParseSkip(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "report": return SkipBehavior.Report;
                case "omit": return SkipBehavior.Omit;
                case "fail": return SkipBehavior.Fail;
                default: throw new ConsoleArgumentException("Option '--skip' must be report, omit, or fail, not '" + value + "'.");
            }
        }
    }
}
