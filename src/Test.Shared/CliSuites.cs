namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Suites covering <see cref="ConsoleResultSink"/> and <see cref="ConsoleRunner"/>.
    /// All output is captured in memory so nothing is written to the real console.
    /// </summary>
    public static class CliSuites
    {
        private const int _FixedColumnsWidth = 18;

        /// <summary>
        /// Suite covering <see cref="ConsoleResultSink"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ConsoleResultSinkSuite()
        {
            const string S = "ConsoleResultSink";

            return new TestSuiteDescriptor(S, "ConsoleResultSink", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "HeaderColumns", "ConsoleResultSink header lists Test, Result, and Runtime columns", () =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);
                    sink.WriteHeader();

                    string[] lines = Lines(writer);
                    TestAssert.Equal(2, lines.Length, "header line count");
                    TestAssert.True(lines[0].StartsWith("Test", StringComparison.Ordinal), "first column is Test");
                    TestAssert.Contains("  Result  ", lines[0], "header");
                    TestAssert.True(lines[0].EndsWith(" Runtime", StringComparison.Ordinal), "last column is Runtime");
                    TestAssert.Equal(lines[0].Length, lines[1].Length, "separator width matches header");
                    TestAssert.Equal(string.Empty, lines[1].Replace("-", string.Empty).Replace(" ", string.Empty), "separator contains only dashes and spaces");
                }),

                CaseBuilder.Async(S, "ResultLabels", "ConsoleResultSink renders PASS, FAIL, and SKIP labels", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);

                    await sink.OnTestCompletedAsync(Result("Passing case", success: true), ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(Result("Failing case", success: false), ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(Result("Skipped case", success: true, skipped: true), ct).ConfigureAwait(false);

                    string[] lines = Lines(writer);
                    TestAssert.Equal(3, lines.Length, "line count");
                    TestAssert.True(lines[0].StartsWith("Passing case", StringComparison.Ordinal), "pass line name");
                    TestAssert.Contains("  PASS  ", lines[0], "pass line");
                    TestAssert.Contains("  FAIL  ", lines[1], "fail line");
                    TestAssert.Contains("  SKIP  ", lines[2], "skip line");
                    TestAssert.True(lines[0].EndsWith("ms", StringComparison.Ordinal), "runtime suffix");
                }),

                CaseBuilder.Async(S, "RuntimeRounding", "ConsoleResultSink rounds runtimes to whole milliseconds away from zero", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);

                    TestResult zero = Result("zero", success: true);
                    TestResult half = Result("half", success: true);
                    half.Duration = TimeSpan.FromTicks(25000);
                    TestResult big = Result("big", success: true);
                    big.Duration = TimeSpan.FromMilliseconds(1234);

                    await sink.OnTestCompletedAsync(zero, ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(half, ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(big, ct).ConfigureAwait(false);

                    string[] lines = Lines(writer);
                    TestAssert.True(lines[0].EndsWith(" 0ms", StringComparison.Ordinal), "0ms: " + lines[0]);
                    TestAssert.True(lines[1].EndsWith(" 3ms", StringComparison.Ordinal), "2.5ms rounds to 3ms: " + lines[1]);
                    TestAssert.True(lines[2].EndsWith(" 1234ms", StringComparison.Ordinal), "1234ms: " + lines[2]);
                }),

                CaseBuilder.Async(S, "CollectsResultsInOrder", "ConsoleResultSink AllResults collects every result in order", async ct =>
                {
                    ConsoleResultSink sink = new ConsoleResultSink(new StringWriter(), useColor: false);
                    TestResult a = Result("a", success: true);
                    TestResult b = Result("b", success: false);

                    TestAssert.Equal(0, sink.AllResults.Count, "initial count");
                    await sink.OnTestCompletedAsync(a, ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(b, ct).ConfigureAwait(false);

                    TestAssert.Equal(2, sink.AllResults.Count, "count");
                    TestAssert.Same(a, sink.AllResults[0], "first");
                    TestAssert.Same(b, sink.AllResults[1], "second");
                }),

                CaseBuilder.Async(S, "NullResultThrows", "ConsoleResultSink rejects a null result", async ct =>
                {
                    ConsoleResultSink sink = new ConsoleResultSink(new StringWriter(), useColor: false);
                    ArgumentNullException ex = await TestAssert.ThrowsAsync<ArgumentNullException>(
                        () => sink.OnTestCompletedAsync(null!, ct).AsTask(), "null result").ConfigureAwait(false);
                    TestAssert.Equal("result", ex.ParamName, "ParamName");
                    TestAssert.Equal(0, sink.AllResults.Count, "nothing recorded");
                }),

                CaseBuilder.Async(S, "SuiteEventsWriteNothing", "ConsoleResultSink suite start and completion events produce no output", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);
                    TestSuiteDescriptor suite = Fixtures.Suite("X");

                    await sink.OnSuiteStartedAsync(suite, ct).ConfigureAwait(false);
                    await sink.OnSuiteCompletedAsync(suite, new TestRunSummary(), ct).ConfigureAwait(false);

                    TestAssert.Equal(string.Empty, writer.ToString(), "output");
                }),

                CaseBuilder.Async(S, "LongNamesTruncated", "ConsoleResultSink truncates long display names with an ellipsis", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);
                    sink.WriteHeader();
                    int width = Lines(writer)[0].Length - _FixedColumnsWidth;

                    StringWriter resultWriter = new StringWriter();
                    ConsoleResultSink resultSink = new ConsoleResultSink(resultWriter, useColor: false);
                    string longName = new string('x', width + 25);
                    await resultSink.OnTestCompletedAsync(Result(longName, success: true), ct).ConfigureAwait(false);

                    string line = Lines(resultWriter)[0];
                    string description = line.Substring(0, width);
                    TestAssert.True(description.EndsWith("...", StringComparison.Ordinal), "ellipsis");
                    TestAssert.Equal(new string('x', width - 3) + "...", description, "truncated description");
                    TestAssert.Equal(width + _FixedColumnsWidth, line.Length, "line width");
                }),

                CaseBuilder.Async(S, "ExactWidthNotTruncated", "ConsoleResultSink does not truncate a name that exactly fits", async ct =>
                {
                    StringWriter headerWriter = new StringWriter();
                    new ConsoleResultSink(headerWriter, useColor: false).WriteHeader();
                    int width = Lines(headerWriter)[0].Length - _FixedColumnsWidth;

                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);
                    string name = new string('y', width);
                    await sink.OnTestCompletedAsync(Result(name, success: true), ct).ConfigureAwait(false);

                    TestAssert.True(Lines(writer)[0].StartsWith(name + "  PASS", StringComparison.Ordinal), "full name rendered");
                }),

                CaseBuilder.Async(S, "EmptyNamePadded", "ConsoleResultSink pads an empty display name to the column width", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);
                    await sink.OnTestCompletedAsync(Result(string.Empty, success: true), ct).ConfigureAwait(false);

                    string line = Lines(writer)[0];
                    int width = line.IndexOf("  PASS", StringComparison.Ordinal);
                    TestAssert.True(width > 0, "PASS label present after padding");
                    TestAssert.Equal(string.Empty, line.Substring(0, width).Trim(), "description column is blank");
                }),

                CaseBuilder.Sync(S, "OverallPass", "ConsoleResultSink overall summary for a passing run has no failure listing", () =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);
                    sink.WriteOverall(TimeSpan.FromMilliseconds(23), 3, 2, 0, 1);

                    string output = writer.ToString();
                    string[] lines = Lines(writer);
                    TestAssert.Equal(string.Empty, lines[0], "leading blank line");
                    TestAssert.True(lines[1].StartsWith("OVERALL", StringComparison.Ordinal), "OVERALL row");
                    TestAssert.Contains("  PASS  ", lines[1], "OVERALL status");
                    TestAssert.True(lines[1].EndsWith("23ms", StringComparison.Ordinal), "OVERALL runtime");
                    TestAssert.Equal("Total: 3  Passed: 2  Failed: 0  Skipped: 1", lines[2], "totals line");
                    TestAssert.DoesNotContain("Failed Tests:", output, "output");
                }),

                CaseBuilder.Async(S, "OverallFailListsFailures", "ConsoleResultSink overall summary lists only failed tests and their messages", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    ConsoleResultSink sink = new ConsoleResultSink(writer, useColor: false);

                    TestResult failed = Result("Broken thing", success: false);
                    failed.Message = "Expected 400 but got 200";
                    TestResult failedNoMessage = Result("Silent failure", success: false);
                    TestResult skipped = Result("Skipped thing", success: true, skipped: true);
                    skipped.Message = "not ready";
                    await sink.OnTestCompletedAsync(Result("Good thing", success: true), ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(failed, ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(failedNoMessage, ct).ConfigureAwait(false);
                    await sink.OnTestCompletedAsync(skipped, ct).ConfigureAwait(false);

                    writer.GetStringBuilder().Clear();
                    sink.WriteOverall(TimeSpan.FromMilliseconds(5), 4, 1, 2, 1);

                    string[] lines = Lines(writer);
                    TestAssert.Contains("  FAIL  ", lines[1], "OVERALL status");
                    TestAssert.Equal("Total: 4  Passed: 1  Failed: 2  Skipped: 1", lines[2], "totals line");
                    TestAssert.Equal("Failed Tests:", lines[3], "failure header");
                    TestAssert.Equal("  Broken thing", lines[4], "first failure name");
                    TestAssert.Equal("    Expected 400 but got 200", lines[5], "first failure message");
                    TestAssert.Equal("  Silent failure", lines[6], "second failure name without message");
                    TestAssert.Equal(7, lines.Length, "no further lines");
                    TestAssert.DoesNotContain("Good thing", writer.ToString(), "passing test not listed");
                    TestAssert.DoesNotContain("Skipped thing", writer.ToString(), "skipped test not listed");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="ConsoleRunner"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ConsoleRunnerSuite()
        {
            const string S = "ConsoleRunner";

            return new TestSuiteDescriptor(S, "ConsoleRunner", new List<TestCaseDescriptor>
            {
                CaseBuilder.Async(S, "NullSuitesThrows", "ConsoleRunner rejects null suites", async ct =>
                {
                    ArgumentNullException ex = await TestAssert.ThrowsAsync<ArgumentNullException>(
                        () => ConsoleRunner.RunAsync(null!, QuietSink(), null, ct), "null suites").ConfigureAwait(false);
                    TestAssert.Equal("suites", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Async(S, "AllPassReturnsZero", "ConsoleRunner returns exit code 0 when all tests pass", async ct =>
                {
                    int exitCode = await ConsoleRunner.RunAsync(
                        Suites(Fixtures.Suite("X", Fixtures.Passing("X", "A"), Fixtures.Passing("X", "B"))),
                        QuietSink(), null, ct).ConfigureAwait(false);
                    TestAssert.Equal(0, exitCode, "exit code");
                }),

                CaseBuilder.Async(S, "AnyFailReturnsOne", "ConsoleRunner returns exit code 1 when any test fails", async ct =>
                {
                    int exitCode = await ConsoleRunner.RunAsync(
                        Suites(
                            Fixtures.Suite("X", Fixtures.Passing("X", "A")),
                            Fixtures.Suite("Y", Fixtures.Failing("Y", "B", "b"))),
                        QuietSink(), null, ct).ConfigureAwait(false);
                    TestAssert.Equal(1, exitCode, "exit code");
                }),

                CaseBuilder.Async(S, "OnlySkippedReturnsZero", "ConsoleRunner returns exit code 0 when tests are only skipped", async ct =>
                {
                    int exitCode = await ConsoleRunner.RunAsync(
                        Suites(Fixtures.Suite("X", Fixtures.Skipped("X", "A"))),
                        QuietSink(), null, ct).ConfigureAwait(false);
                    TestAssert.Equal(0, exitCode, "exit code");
                }),

                CaseBuilder.Async(S, "EmptyRun", "ConsoleRunner handles an empty suite list", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    int exitCode = await ConsoleRunner.RunAsync(
                        new List<TestSuiteDescriptor>(), new ConsoleResultSink(writer, useColor: false), null, ct).ConfigureAwait(false);
                    TestAssert.Equal(0, exitCode, "exit code");
                    TestAssert.Contains("Total: 0  Passed: 0  Failed: 0  Skipped: 0", writer.ToString(), "output");
                }),

                CaseBuilder.Async(S, "AggregatesAcrossSuites", "ConsoleRunner writes header, every result, and totals across suites", async ct =>
                {
                    StringWriter writer = new StringWriter();
                    int exitCode = await ConsoleRunner.RunAsync(
                        Suites(
                            Fixtures.Suite("X", Fixtures.Passing("X", "A"), Fixtures.Skipped("X", "B")),
                            Fixtures.Suite("Y", Fixtures.Failing("Y", "C", "c failed"), Fixtures.Passing("Y", "D"))),
                        new ConsoleResultSink(writer, useColor: false), null, ct).ConfigureAwait(false);

                    string[] lines = Lines(writer);
                    TestAssert.Equal(1, exitCode, "exit code");
                    TestAssert.True(lines[0].StartsWith("Test", StringComparison.Ordinal), "header first");
                    TestAssert.True(lines[2].StartsWith("A display", StringComparison.Ordinal), "first result");
                    TestAssert.True(lines[3].StartsWith("B display", StringComparison.Ordinal), "second result");
                    TestAssert.True(lines[4].StartsWith("C display", StringComparison.Ordinal), "third result");
                    TestAssert.True(lines[5].StartsWith("D display", StringComparison.Ordinal), "fourth result");
                    TestAssert.Contains("Total: 4  Passed: 2  Failed: 1  Skipped: 1", writer.ToString(), "totals");
                    TestAssert.Contains("    c failed", writer.ToString(), "failure message");
                }),

                CaseBuilder.Async(S, "ExportsJson", "ConsoleRunner writes camelCase JSON results to the results path", async ct =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-test-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        TestCaseDescriptor slow = new TestCaseDescriptor("X", "Slow", "Slow case", async t =>
                        {
                            await Task.Delay(20, t).ConfigureAwait(false);
                        });
                        await ConsoleRunner.RunAsync(
                            Suites(Fixtures.Suite("X", slow, Fixtures.Failing("X", "Bad", "bad message"), Fixtures.Skipped("X", "Later", "reason"))),
                            QuietSink(), path, ct).ConfigureAwait(false);

                        TestAssert.True(File.Exists(path), "results file created");
                        string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            JsonElement root = doc.RootElement;
                            TestAssert.Equal(JsonValueKind.Array, root.ValueKind, "root kind");
                            TestAssert.Equal(3, root.GetArrayLength(), "entry count");

                            JsonElement first = root[0];
                            TestAssert.Equal("X.Slow", first.GetProperty("testId").GetString(), "testId");
                            TestAssert.Equal("X", first.GetProperty("suiteId").GetString(), "suiteId");
                            TestAssert.Equal("Slow", first.GetProperty("caseId").GetString(), "caseId");
                            TestAssert.Equal("Slow case", first.GetProperty("displayName").GetString(), "displayName");
                            TestAssert.True(first.GetProperty("success").GetBoolean(), "success");
                            TestAssert.False(first.GetProperty("skipped").GetBoolean(), "skipped");
                            TestAssert.True(first.GetProperty("durationMs").GetInt64() >= 15, "durationMs reflects elapsed time");
                            TestAssert.Equal(JsonValueKind.Null, first.GetProperty("message").ValueKind, "message is null on success");

                            JsonElement second = root[1];
                            TestAssert.False(second.GetProperty("success").GetBoolean(), "failed success");
                            TestAssert.Equal("bad message", second.GetProperty("message").GetString(), "failed message");

                            JsonElement third = root[2];
                            TestAssert.True(third.GetProperty("skipped").GetBoolean(), "skipped flag");
                            TestAssert.True(third.GetProperty("success").GetBoolean(), "skipped success");
                            TestAssert.Equal("reason", third.GetProperty("message").GetString(), "skip message");
                            TestAssert.Equal(0L, third.GetProperty("durationMs").GetInt64(), "skip duration");

                            TestAssert.False(first.TryGetProperty("TestId", out JsonElement _), "PascalCase property absent");
                        }
                    }
                    finally
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                }),

                CaseBuilder.Async(S, "EmptyResultsPathSkipsExport", "ConsoleRunner does not export when the results path is empty", async ct =>
                {
                    int exitCode = await ConsoleRunner.RunAsync(
                        Suites(Fixtures.Suite("X", Fixtures.Passing("X", "A"))),
                        QuietSink(), string.Empty, ct).ConfigureAwait(false);
                    TestAssert.Equal(0, exitCode, "exit code");
                }),

                CaseBuilder.Async(S, "ExportOverwritesExistingFile", "ConsoleRunner overwrites an existing results file", async ct =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-test-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        await File.WriteAllTextAsync(path, "stale content that is not json", ct).ConfigureAwait(false);
                        await ConsoleRunner.RunAsync(
                            Suites(Fixtures.Suite("X", Fixtures.Passing("X", "A"))),
                            QuietSink(), path, ct).ConfigureAwait(false);

                        string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            TestAssert.Equal(1, doc.RootElement.GetArrayLength(), "entry count");
                        }
                    }
                    finally
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                }),

                CaseBuilder.Async(S, "InvalidResultsPathThrows", "ConsoleRunner propagates an error when the results directory does not exist", async ct =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "touchstone-missing-" + Guid.NewGuid().ToString("N"), "results.json");
                    await TestAssert.ThrowsAsync<DirectoryNotFoundException>(
                        () => ConsoleRunner.RunAsync(
                            Suites(Fixtures.Suite("X", Fixtures.Passing("X", "A"))),
                            QuietSink(), path, ct),
                        "missing directory").ConfigureAwait(false);
                }),

                CaseBuilder.Async(S, "TokenForwarded", "ConsoleRunner forwards the cancellation token to test cases", async _ =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        CancellationToken seen = CancellationToken.None;
                        await ConsoleRunner.RunAsync(
                            Suites(Fixtures.Suite("X", Fixtures.Passing("X", "A", t => seen = t))),
                            QuietSink(), null, cts.Token).ConfigureAwait(false);
                        TestAssert.True(seen == cts.Token, "case received the caller token");
                    }
                }),
            });
        }

        private static ConsoleResultSink QuietSink()
        {
            return new ConsoleResultSink(new StringWriter(), useColor: false);
        }

        private static IReadOnlyList<TestSuiteDescriptor> Suites(params TestSuiteDescriptor[] suites)
        {
            return new List<TestSuiteDescriptor>(suites);
        }

        private static TestResult Result(string displayName, bool success, bool skipped = false)
        {
            return new TestResult
            {
                TestId = "S." + displayName,
                SuiteId = "S",
                CaseId = displayName,
                DisplayName = displayName,
                Success = success,
                Skipped = skipped,
            };
        }

        private static string[] Lines(StringWriter writer)
        {
            string text = writer.ToString().Replace("\r\n", "\n");
            if (text.EndsWith("\n", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1);
            return text.Length == 0 ? Array.Empty<string>() : text.Split('\n');
        }
    }
}
