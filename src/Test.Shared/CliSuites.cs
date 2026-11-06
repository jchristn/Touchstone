namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.16 and 11.17: the console sink, the console runner, arguments, and exit codes.
    /// </summary>
    public static class CliSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return ConsoleSinkSuite();
            yield return ConsoleRunnerSuite();
            yield return ArgumentSuite();
        }

        internal static async Task<string> RunToText(IReadOnlyList<TestSuiteDescriptor> suites, Action<ConsoleRunnerOptions>? configure, CancellationToken ct, StrongBox<int>? code = null)
        {
            using (StringWriter writer = new StringWriter())
            {
                ConsoleRunnerOptions options = new ConsoleRunnerOptions { Writer = writer, UseColor = false };
                configure?.Invoke(options);
                int exit = await ConsoleRunner.RunAsync(suites, options, ct);
                if (code != null) code.Value = exit;
                return writer.ToString();
            }
        }

        private static TestSuiteDescriptor ConsoleSinkSuite()
        {
            const string S = "ConsoleSink";
            return new TestSuiteDescriptor(S, "Console sink", new[]
            {
                Check.Async(S, "CSINK-01", "The header lists Test, Result, and Runtime", async ct =>
                {
                    string text = await RunToText(new[] { Fixtures.Suite("C") }, null, ct);
                    string header = text.Split('\n')[0];
                    TestAssert.True(header.StartsWith("Test", StringComparison.Ordinal), "test column");
                    TestAssert.Contains("Result", header, "result column");
                    TestAssert.Contains("Runtime", header, "runtime column");
                }),
                Check.Async(S, "CSINK-02", "Rows render PASS, FAIL, SKIP, CANCELED, and NOT RUN", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        string text = await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A"), Fixtures.Failing("C", "B"), Fixtures.Skipped("C", "D"),
                            Fixtures.CancelsRun("C", "E", source), Fixtures.Passing("C", "F")) }, null, source.Token);
                        foreach (string label in new[] { "PASS", "FAIL", "SKIP", "CANCELED", "NOT RUN" })
                            TestAssert.Contains(label, text, label);
                    }
                }),
                Check.Async(S, "CSINK-03", "Runtimes round to whole milliseconds, away from zero", async ct =>
                {
                    using (StringWriter writer = new StringWriter())
                    {
                        ConsoleResultSink sink = new ConsoleResultSink(new ConsoleRunnerOptions { Writer = writer, UseColor = false });
                        await sink.OnCaseCompletedAsync(new TestResult("C", "A", "Half", TestOutcome.Passed, TimeSpan.FromMilliseconds(2.5)), ct);
                        await sink.OnCaseCompletedAsync(new TestResult("C", "B", "Low", TestOutcome.Passed, TimeSpan.FromMilliseconds(2.4)), ct);
                        string[] lines = writer.ToString().Split('\n');
                        TestAssert.True(lines[0].TrimEnd().EndsWith("3ms", StringComparison.Ordinal), "2.5 rounds to 3");
                        TestAssert.True(lines[1].TrimEnd().EndsWith("2ms", StringComparison.Ordinal), "2.4 rounds to 2");
                    }
                }),
                Check.Async(S, "CSINK-04", "Long names are truncated with an ellipsis; names that fit are not", async ct =>
                {
                    using (StringWriter writer = new StringWriter())
                    {
                        ConsoleResultSink sink = new ConsoleResultSink(new ConsoleRunnerOptions { Writer = writer, UseColor = false });
                        string fits = new string('a', sink.DescriptionColumnWidth);
                        string tooLong = new string('b', sink.DescriptionColumnWidth + 5);
                        await sink.OnCaseCompletedAsync(new TestResult("C", "A", fits, TestOutcome.Passed, TimeSpan.Zero), ct);
                        await sink.OnCaseCompletedAsync(new TestResult("C", "B", tooLong, TestOutcome.Passed, TimeSpan.Zero), ct);
                        string[] lines = writer.ToString().Split('\n');
                        TestAssert.True(lines[0].StartsWith(fits + "  ", StringComparison.Ordinal), "exact fit kept");
                        TestAssert.True(lines[1].StartsWith(new string('b', sink.DescriptionColumnWidth - 3) + "...  ", StringComparison.Ordinal), "truncated");
                    }
                }),
                Check.Sync(S, "CSINK-05", "A non-console writer uses the default width of 100", () =>
                {
                    using (StringWriter writer = new StringWriter())
                    {
                        TestAssert.Equal(100, new ConsoleResultSink(new ConsoleRunnerOptions { Writer = writer }).DescriptionColumnWidth, "width");
                    }
                }),
                Check.Async(S, "CSINK-06", "The overall section lists failed and canceled tests, then lifecycle errors", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        string text = await RunToText(new[]
                        {
                            Fixtures.HookedSuite("C", null, _ => throw new InvalidOperationException("teardown broke"), Fixtures.Passing("C", "Ok"), Fixtures.Failing("C", "Bad", "bad message")),
                            Fixtures.Suite("D", Fixtures.CancelsRun("D", "Stop", source)),
                        }, null, source.Token);
                        string overall = text.Substring(text.IndexOf("OVERALL", StringComparison.Ordinal));
                        TestAssert.Contains("Failed Tests:", overall, "failed header");
                        TestAssert.Contains("Bad display", overall, "failed case");
                        TestAssert.Contains("bad message", overall, "failure message");
                        TestAssert.Contains("Stop display", overall, "canceled case");
                        TestAssert.DoesNotContain("Ok display", overall, "passing case");
                        TestAssert.True(overall.IndexOf("Suite Lifecycle Errors:", StringComparison.Ordinal) > overall.IndexOf("Failed Tests:", StringComparison.Ordinal), "lifecycle after failures");
                        TestAssert.Contains("C after_suite: teardown broke", overall, "lifecycle error");
                    }
                }),
                Check.Async(S, "CSINK-07", "The totals line shows the filtered count when non-zero", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("C", Fixtures.Passing("C", "A", null, new[] { "keep" }), Fixtures.Passing("C", "B"));
                    string filtered = await RunToText(new[] { suite }, o => o.Run.Filter.IncludeTags.Add("keep"), ct);
                    string unfiltered = await RunToText(new[] { suite }, null, ct);
                    TestAssert.Contains("Filtered: 1", filtered, "filtered");
                    TestAssert.DoesNotContain("Filtered", unfiltered, "unfiltered");
                }),
                Check.Async(S, "CSINK-08", "ANSI color is written only when enabled or to a live console", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("C", Fixtures.Passing("C", "A"));
                    string auto = await RunToText(new[] { suite }, o => o.UseColor = null, ct);
                    string forced = await RunToText(new[] { suite }, o => o.UseColor = true, ct);
                    string off = await RunToText(new[] { suite }, o => o.UseColor = false, ct);
                    TestAssert.DoesNotContain("\u001b[", auto, "auto with a custom writer");
                    TestAssert.Contains("\u001b[32mPASS", forced, "forced");
                    TestAssert.DoesNotContain("\u001b[", off, "off");
                }),
                Check.Async(S, "CSINK-09", "The process console color is never changed", async ct =>
                {
                    ConsoleColor before = Console.ForegroundColor;
                    await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A"), Fixtures.Failing("C", "B")) }, o => o.UseColor = true, ct);
                    TestAssert.Equal(before, Console.ForegroundColor, "foreground color");
                }),
                Check.Async(S, "CSINK-10", "ExpandSteps writes live step lines; off writes none", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("C", Fixtures.Scenario("C", "Scn", (c, t) => Task.CompletedTask));
                    string expanded = await RunToText(new[] { suite }, o => o.ExpandSteps = true, ct);
                    string compact = await RunToText(new[] { suite }, null, ct);
                    TestAssert.Contains("WHEN the action runs...", expanded, "step start");
                    TestAssert.DoesNotContain("the action runs", compact, "compact");
                }),
                Check.Async(S, "CSINK-11", "Redact applies to every consumer string written", async ct =>
                {
                    TestCaseDescriptor scenario = new ScenarioBuilder<ScenarioContext>("C", "Scn", "secret scenario", _ => Task.FromResult(new ScenarioContext { FailDisposal = true }))
                        .Then("a", "secret step", (_, _) => throw new InvalidOperationException("secret failure"))
                        .And("b", "secret later", (_, _) => Task.CompletedTask)
                        .Build();
                    string text = await RunToText(new[] { Fixtures.HookedSuite("C", null, _ => throw new InvalidOperationException("secret hook"), scenario) }, o =>
                    {
                        o.ExpandSteps = true;
                        o.Redact = s => s.Replace("secret", "[x]", StringComparison.Ordinal);
                    }, ct);
                    TestAssert.DoesNotContain("secret", text, "redacted");
                    TestAssert.Contains("[x] step", text, "redaction applied");
                }),
                Check.Sync(S, "CSINK-12", "A null Redact is rejected", () =>
                {
                    TestAssert.Throws<ArgumentNullException>(() => new ConsoleRunnerOptions().Redact = null!, "redact");
                }),
            });
        }

        private static TestSuiteDescriptor ConsoleRunnerSuite()
        {
            const string S = "ConsoleRunner";
            return new TestSuiteDescriptor(S, "Console runner", new[]
            {
                Check.Async(S, "CRUN-01", "Null suites are rejected", async ct =>
                {
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => ConsoleRunner.RunAsync(null!, null, ct), "options overload");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => ConsoleRunner.RunMainAsync(null!, Array.Empty<string>(), ct), "main overload");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => ConsoleRunner.RunMainAsync(Array.Empty<TestSuiteDescriptor>(), null!, ct), "null args");
                }),
                Check.Async(S, "CRUN-02", "Exit 0 for passing, skipped-only, and empty runs", async ct =>
                {
                    StrongBox<int> code = new StrongBox<int>();
                    await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")) }, null, ct, code);
                    TestAssert.Equal(ConsoleRunner.ExitSuccess, code.Value, "passing");
                    await RunToText(new[] { Fixtures.Suite("C", Fixtures.Skipped("C", "A")) }, null, ct, code);
                    TestAssert.Equal(0, code.Value, "skipped");
                    await RunToText(Array.Empty<TestSuiteDescriptor>(), null, ct, code);
                    TestAssert.Equal(0, code.Value, "empty");
                }),
                Check.Async(S, "CRUN-03", "Exit 1 when a case fails", async ct =>
                {
                    StrongBox<int> code = new StrongBox<int>();
                    await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A"), Fixtures.Failing("C", "B")) }, null, ct, code);
                    TestAssert.Equal(ConsoleRunner.ExitFailure, code.Value, "code");
                    TestAssert.Equal(1, ConsoleRunner.ExitFailure, "constant");
                }),
                Check.Async(S, "CRUN-04", "Exit 1 for a lifecycle error with no failed case", async ct =>
                {
                    StrongBox<int> code = new StrongBox<int>();
                    await RunToText(new[] { Fixtures.HookedSuite("C", null, _ => throw new InvalidOperationException("x"), Fixtures.Passing("C", "A")) }, null, ct, code);
                    TestAssert.Equal(1, code.Value, "code");
                }),
                Check.Async(S, "CRUN-05", "Exit 2 when canceled", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        StrongBox<int> code = new StrongBox<int>();
                        await RunToText(new[] { Fixtures.Suite("C", Fixtures.CancelsRun("C", "A", source)) }, null, source.Token, code);
                        TestAssert.Equal(ConsoleRunner.ExitCanceled, code.Value, "code");
                        TestAssert.Equal(2, ConsoleRunner.ExitCanceled, "constant");
                    }
                }),
                Check.Sync(S, "CRUN-06", "Invalid arguments map to exit 3 without running tests", () =>
                {
                    TestAssert.Equal(3, ConsoleRunner.ExitInvalidArguments, "constant");
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--bogus" }), "parse");
                    TestAssert.Contains("--tag", ConsoleRunnerOptions.Usage, "usage text");
                    TestAssert.Contains("3 invalid arguments", ConsoleRunnerOptions.Usage, "usage documents exit codes");
                }),
                Check.Async(S, "CRUN-07", "Writes the header, every result, and totals across suites", async ct =>
                {
                    string text = await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")), Fixtures.Suite("D", Fixtures.Passing("D", "B")) }, null, ct);
                    TestAssert.Contains("A display", text, "first");
                    TestAssert.Contains("B display", text, "second");
                    TestAssert.Contains("Total: 2  Passed: 2  Failed: 0  Skipped: 0", text, "totals");
                }),
                Check.Async(S, "CRUN-08", "JSON is written when a path is set and overwritten on rerun", async ct =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A"), Fixtures.Passing("C", "B")) }, o => o.JsonResultsPath = path, ct);
                        string first = await File.ReadAllTextAsync(path, ct);
                        await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")) }, o => o.JsonResultsPath = path, ct);
                        string second = await File.ReadAllTextAsync(path, ct);
                        TestAssert.Contains("C.B", first, "first run");
                        TestAssert.DoesNotContain("C.B", second, "overwritten");
                        await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")) }, o => o.JsonResultsPath = string.Empty, ct);
                    }
                    finally
                    {
                        File.Delete(path);
                    }
                }),
                Check.Async(S, "CRUN-09", "HTML is written when a path is set", async ct =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-" + Guid.NewGuid().ToString("N") + ".html");
                    try
                    {
                        await RunToText(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")) }, o => { o.HtmlReportPath = path; o.RunId = "nightly"; }, ct);
                        string html = await File.ReadAllTextAsync(path, ct);
                        TestAssert.Contains("<!doctype html>", html, "document");
                        TestAssert.Contains("nightly", html, "run id");
                    }
                    finally
                    {
                        File.Delete(path);
                    }
                }),
                Check.Async(S, "CRUN-10", "A missing results directory fails after the console summary is written", async ct =>
                {
                    using (StringWriter writer = new StringWriter())
                    {
                        string path = Path.Combine(Path.GetTempPath(), "touchstone-missing-" + Guid.NewGuid().ToString("N"), "results.json");
                        ConsoleRunnerOptions options = new ConsoleRunnerOptions { Writer = writer, UseColor = false, JsonResultsPath = path };
                        await TestAssert.ThrowsAsync<DirectoryNotFoundException>(() => ConsoleRunner.RunAsync(new[] { Fixtures.Suite("C", Fixtures.Passing("C", "A")) }, options, ct), "export");
                        TestAssert.Contains("OVERALL", writer.ToString(), "summary written first");
                    }
                }),
                Check.Async(S, "CRUN-11", "Additional sinks receive every callback", async ct =>
                {
                    RecordingSink extra = new RecordingSink();
                    await RunToText(new[] { Fixtures.Suite("C", Fixtures.Scenario("C", "Scn", (c, t) => Task.CompletedTask)) }, o => o.AdditionalSinks.Add(extra), ct);
                    TestAssert.Equal("run_started:1|suite_started:C|case_started:C.Scn|step_started:C.Scn/act|step_completed:C.Scn/act=Passed|case_completed:C.Scn=Passed|suite_completed:C|run_completed:run",
                        string.Join("|", extra.Events), "events");
                }),
                Check.Async(S, "CRUN-12", "Run options are honored", async ct =>
                {
                    StrongBox<int> code = new StrongBox<int>();
                    string text = await RunToText(new[] { Fixtures.Suite("C", Fixtures.Failing("C", "A"), Fixtures.Passing("C", "B", null, new[] { "x" }), Fixtures.Skipped("C", "D")) }, o =>
                    {
                        o.Run.StopOnFirstFailure = true;
                        o.Run.SkipBehavior = SkipBehavior.Omit;
                    }, ct, code);
                    TestAssert.Contains("NOT RUN", text, "stopped");
                    TestAssert.Contains("Filtered: 1", text, "omitted");
                    TestAssert.Equal(1, code.Value, "code");
                }),
            });
        }

        private static TestSuiteDescriptor ArgumentSuite()
        {
            const string S = "Arguments";
            return new TestSuiteDescriptor(S, "Command-line arguments", new[]
            {
                Check.Sync(S, "ARGS-01", "No arguments parse to defaults", () =>
                {
                    ConsoleRunnerOptions o = ConsoleRunnerOptions.Parse(Array.Empty<string>());
                    TestAssert.True(o.Run.Filter.IsEmpty, "filter");
                    TestAssert.Equal(SkipBehavior.Report, o.Run.SkipBehavior, "skip");
                    TestAssert.False(o.Run.StopOnFirstFailure, "stop");
                    TestAssert.False(o.ExpandSteps, "expand");
                    TestAssert.Null(o.UseColor, "color");
                    TestAssert.Null(o.JsonResultsPath, "json");
                    TestAssert.Null(o.HtmlReportPath, "html");
                    TestAssert.False(o.ShowHelp, "help");
                }),
                Check.Sync(S, "ARGS-02", "--tag and --exclude-tag accept repeated and comma-separated values", () =>
                {
                    ConsoleRunnerOptions o = ConsoleRunnerOptions.Parse(new[] { "--tag", "a,b", "--tag", "c", "--exclude-tag", "d, e" });
                    TestAssert.Equal("a,b,c", string.Join(",", o.Run.Filter.IncludeTags), "include");
                    TestAssert.Equal("d,e", string.Join(",", o.Run.Filter.ExcludeTags), "exclude");
                }),
                Check.Sync(S, "ARGS-03", "--suite and --test populate the filter", () =>
                {
                    ConsoleRunnerOptions o = ConsoleRunnerOptions.Parse(new[] { "--suite", "A,B", "--test", "A.One" });
                    TestAssert.Equal("A,B", string.Join(",", o.Run.Filter.SuiteIds), "suites");
                    TestAssert.Equal("A.One", string.Join(",", o.Run.Filter.TestIds), "tests");
                }),
                Check.Sync(S, "ARGS-04", "--skip accepts report, omit, and fail in any case", () =>
                {
                    TestAssert.Equal(SkipBehavior.Report, ConsoleRunnerOptions.Parse(new[] { "--skip", "REPORT" }).Run.SkipBehavior, "report");
                    TestAssert.Equal(SkipBehavior.Omit, ConsoleRunnerOptions.Parse(new[] { "--skip", "omit" }).Run.SkipBehavior, "omit");
                    TestAssert.Equal(SkipBehavior.Fail, ConsoleRunnerOptions.Parse(new[] { "--skip", "Fail" }).Run.SkipBehavior, "fail");
                }),
                Check.Sync(S, "ARGS-05", "Flags set their options", () =>
                {
                    ConsoleRunnerOptions o = ConsoleRunnerOptions.Parse(new[] { "--stop-on-failure", "--expand", "--no-color" });
                    TestAssert.True(o.Run.StopOnFirstFailure, "stop");
                    TestAssert.True(o.ExpandSteps, "expand");
                    TestAssert.Equal(false, o.UseColor, "color");
                }),
                Check.Sync(S, "ARGS-06", "--results and --html set the paths", () =>
                {
                    ConsoleRunnerOptions o = ConsoleRunnerOptions.Parse(new[] { "--results", "r.json", "--html", "r.html" });
                    TestAssert.Equal("r.json", o.JsonResultsPath, "json");
                    TestAssert.Equal("r.html", o.HtmlReportPath, "html");
                }),
                Check.Sync(S, "ARGS-07", "An unknown option names itself", () =>
                {
                    TestAssert.Contains("--nope", TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--nope" }), "unknown").Message, "message");
                }),
                Check.Sync(S, "ARGS-08", "An option missing its value is rejected", () =>
                {
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--tag" }), "end of args");
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--results", "--expand" }), "followed by option");
                }),
                Check.Sync(S, "ARGS-09", "Invalid --skip values and invalid tags are rejected", () =>
                {
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--skip", "maybe" }), "skip");
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--tag", "bad tag" }), "tag");
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--suite", "a.b" }), "suite");
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new[] { "--tag", "a,,b" }), "empty item");
                }),
                Check.Sync(S, "ARGS-10", "--help sets ShowHelp", () =>
                {
                    TestAssert.True(ConsoleRunnerOptions.Parse(new[] { "--help" }).ShowHelp, "--help");
                    TestAssert.True(ConsoleRunnerOptions.Parse(new[] { "-h" }).ShowHelp, "-h");
                }),
                Check.Sync(S, "ARGS-11", "A null argument array is rejected", () =>
                {
                    TestAssert.Throws<ArgumentNullException>(() => ConsoleRunnerOptions.Parse(null!), "null");
                    TestAssert.Throws<ConsoleArgumentException>(() => ConsoleRunnerOptions.Parse(new string[] { null! }), "null element");
                }),
            });
        }
    }
}
