namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.18 to 11.20: the JSON report, the HTML report, and diagnostics.
    /// </summary>
    public static class ReportSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return JsonSuite();
            yield return HtmlSuite();
            yield return DiagnosticsSuite();
        }

        private static async Task<string> Export(Func<string, ReportOptions, Task> write, ReportOptions? options = null)
        {
            string path = Path.Combine(Path.GetTempPath(), "touchstone-" + Guid.NewGuid().ToString("N"));
            try
            {
                await write(path, options ?? new ReportOptions());
                return await File.ReadAllTextAsync(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static async Task<RunCapture> Run(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options, CancellationToken ct)
        {
            TestResultCollector collector = new TestResultCollector();
            TestRunSummary summary = await TestExecutor.RunAsync(suites, collector, options, ct);
            return new RunCapture(summary, collector.Results);
        }

        private static async Task<RunCapture> MixedRun(CancellationToken ct)
        {
            using (CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                TestRunOptions options = new TestRunOptions();
                options.Filter.ExcludeTags.Add("off");
                TestCaseDescriptor scenario = new ScenarioBuilder<ScenarioContext>("R", "Scn", "Scenario", _ => Task.FromResult(new ScenarioContext { FailDisposal = true }))
                    .WithMetadata(new ScenarioMetadata("Feature X", new[] { "REQ-1" }, "ex-1"))
                    .Given("a", "given", (_, _) => Task.CompletedTask)
                    .When("b", "when", (_, _) => throw new InvalidOperationException("step failed"))
                    .Then("c", "then", (_, _) => Task.CompletedTask)
                    .Build();
                return await Run(new[]
                {
                    Fixtures.HookedSuite("R", null, _ => throw new InvalidOperationException("teardown failed"),
                        Fixtures.Passing("R", "Pass", null, new[] { "fast" }), Fixtures.Failing("R", "Fail"), Fixtures.Skipped("R", "Skip"), scenario,
                        Fixtures.Passing("R", "Off", null, new[] { "off" })),
                    Fixtures.Suite("T", Fixtures.CancelsRun("T", "Cancel", source), Fixtures.Passing("T", "After")),
                }, options, source.Token);
            }
        }

        private static TestSuiteDescriptor JsonSuite()
        {
            const string S = "Json";
            Func<RunCapture, Task<string>> json = run => Export((p, o) => JsonReport.WriteAsync(run.Summary, run.Results, p, o));

            return new TestSuiteDescriptor(S, "JSON report", new[]
            {
                Check.Async(S, "JSON-01", "The root has schemaVersion, tool, run, and results", async ct =>
                {
                    using (JsonDocument doc = JsonDocument.Parse(await json(await MixedRun(ct))))
                    {
                        JsonElement root = doc.RootElement;
                        TestAssert.Equal(JsonValueKind.Object, root.ValueKind, "root kind");
                        TestAssert.Equal("1.0", root.GetProperty("schemaVersion").GetString(), "schema version");
                        TestAssert.Equal("Touchstone", root.GetProperty("tool").GetProperty("name").GetString(), "tool");
                        TestAssert.Equal(JsonValueKind.Array, root.GetProperty("results").ValueKind, "results");
                    }
                }),
                Check.Async(S, "JSON-02", "tool.version matches the Core informational version", async ct =>
                {
                    string expected = typeof(TestExecutor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
                    using (JsonDocument doc = JsonDocument.Parse(await json(await MixedRun(ct))))
                    {
                        TestAssert.Equal(expected, doc.RootElement.GetProperty("tool").GetProperty("version").GetString(), "version");
                    }
                }),
                Check.Async(S, "JSON-03", "run.summary matches the returned summary", async ct =>
                {
                    RunCapture run = await MixedRun(ct);
                    using (JsonDocument doc = JsonDocument.Parse(await json(run)))
                    {
                        JsonElement s = doc.RootElement.GetProperty("run").GetProperty("summary");
                        TestAssert.Equal(run.Summary.Total, s.GetProperty("total").GetInt32(), "total");
                        TestAssert.Equal(run.Summary.Passed, s.GetProperty("passed").GetInt32(), "passed");
                        TestAssert.Equal(run.Summary.Failed, s.GetProperty("failed").GetInt32(), "failed");
                        TestAssert.Equal(run.Summary.Skipped, s.GetProperty("skipped").GetInt32(), "skipped");
                        TestAssert.Equal(run.Summary.Canceled, s.GetProperty("canceled").GetInt32(), "canceled");
                        TestAssert.Equal(run.Summary.NotRun, s.GetProperty("notRun").GetInt32(), "not run");
                        TestAssert.Equal(1, s.GetProperty("filtered").GetInt32(), "filtered");
                    }
                }),
                Check.Async(S, "JSON-04", "run.canceled and run.lifecycleErrors are populated", async ct =>
                {
                    using (JsonDocument doc = JsonDocument.Parse(await json(await MixedRun(ct))))
                    {
                        JsonElement run = doc.RootElement.GetProperty("run");
                        TestAssert.True(run.GetProperty("canceled").GetBoolean(), "canceled");
                        JsonElement error = run.GetProperty("lifecycleErrors")[0];
                        TestAssert.Equal("after_suite", error.GetProperty("stage").GetString(), "stage");
                        TestAssert.Equal("teardown failed", error.GetProperty("message").GetString(), "message");
                    }
                }),
                Check.Async(S, "JSON-05", "Every outcome serializes as its lowercase name", async ct =>
                {
                    using (JsonDocument doc = JsonDocument.Parse(await json(await MixedRun(ct))))
                    {
                        string outcomes = string.Join(",", doc.RootElement.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("outcome").GetString()));
                        TestAssert.Equal("passed,failed,skipped,failed,canceled,not_run", outcomes, "outcomes");
                    }
                }),
                Check.Async(S, "JSON-06", "Step kind and outcome serialize lowercase", async ct =>
                {
                    using (JsonDocument doc = JsonDocument.Parse(await json(await MixedRun(ct))))
                    {
                        JsonElement steps = doc.RootElement.GetProperty("results")[3].GetProperty("steps");
                        TestAssert.Equal("given:passed,when:failed,then:not_run", string.Join(",", steps.EnumerateArray().Select(s => s.GetProperty("kind").GetString() + ":" + s.GetProperty("outcome").GetString())), "steps");
                    }
                }),
                Check.Async(S, "JSON-07", "Result and step durations are both fractional numbers", async ct =>
                {
                    TestResult result = new TestResult("R", "A", "N", TestOutcome.Passed, TimeSpan.FromTicks(15_000), steps: new[]
                    {
                        new ScenarioStepResult("s", "s", ScenarioStepKind.Given, ScenarioStepOutcome.Passed, TimeSpan.FromTicks(5_000)),
                    });
                    string text = await Export((p, o) => JsonReport.WriteAsync(new TestRunSummary(1, 0, 0, 0, 0, 0, TimeSpan.Zero), new[] { result }, p, o));
                    using (JsonDocument doc = JsonDocument.Parse(text))
                    {
                        JsonElement r = doc.RootElement.GetProperty("results")[0];
                        TestAssert.Equal(1.5, r.GetProperty("durationMs").GetDouble(), "result");
                        TestAssert.Equal(0.5, r.GetProperty("steps")[0].GetProperty("durationMs").GetDouble(), "step");
                    }
                }),
                Check.Async(S, "JSON-08", "Property names are camelCase", async ct =>
                {
                    string text = await json(await MixedRun(ct));
                    foreach (string name in new[] { "\"schemaVersion\"", "\"startedUtc\"", "\"durationMs\"", "\"lifecycleErrors\"", "\"notRun\"", "\"testId\"", "\"displayName\"", "\"cleanupErrors\"", "\"stepId\"" })
                        TestAssert.Contains(name, text, name);
                    TestAssert.DoesNotContain("\"TestId\"", text, "PascalCase");
                }),
                Check.Async(S, "JSON-09", "Scenario fields are set for scenarios and empty for ordinary cases", async ct =>
                {
                    using (JsonDocument doc = JsonDocument.Parse(await json(await MixedRun(ct))))
                    {
                        JsonElement ordinary = doc.RootElement.GetProperty("results")[0];
                        JsonElement scenario = doc.RootElement.GetProperty("results")[3];
                        TestAssert.Equal(JsonValueKind.Null, ordinary.GetProperty("feature").ValueKind, "ordinary feature");
                        TestAssert.Equal(0, ordinary.GetProperty("steps").GetArrayLength(), "ordinary steps");
                        TestAssert.Equal("Feature X", scenario.GetProperty("feature").GetString(), "feature");
                        TestAssert.Equal("ex-1", scenario.GetProperty("exampleId").GetString(), "example");
                        TestAssert.Equal("REQ-1", scenario.GetProperty("requirements")[0].GetString(), "requirement");
                        TestAssert.Equal("dispose failure", scenario.GetProperty("cleanupErrors")[0].GetString(), "cleanup");
                        TestAssert.Equal(3, scenario.GetProperty("steps").GetArrayLength(), "steps");
                    }
                }),
                Check.Async(S, "JSON-10", "Redact applies to every consumer string", async ct =>
                {
                    RunCapture run = await MixedRun(ct);
                    string text = await Export((p, o) => JsonReport.WriteAsync(run.Summary, run.Results, p, o), new ReportOptions
                    {
                        Redact = s => s.Replace("R", "*", StringComparison.Ordinal).Replace("fail", "*", StringComparison.Ordinal).Replace("fast", "*", StringComparison.Ordinal).Replace("dispose", "*", StringComparison.Ordinal),
                    });
                    using (JsonDocument doc = JsonDocument.Parse(text))
                    {
                        TestAssert.Equal("*", doc.RootElement.GetProperty("results")[0].GetProperty("suiteId").GetString(), "suite");
                        TestAssert.Equal("*", doc.RootElement.GetProperty("results")[0].GetProperty("tags")[0].GetString(), "tag");
                        TestAssert.Contains("*", doc.RootElement.GetProperty("run").GetProperty("lifecycleErrors")[0].GetProperty("message").GetString(), "lifecycle message");
                        TestAssert.Equal("* *ure", doc.RootElement.GetProperty("results")[3].GetProperty("cleanupErrors")[0].GetString(), "cleanup");
                        TestAssert.Equal("*EQ-1", doc.RootElement.GetProperty("results")[3].GetProperty("requirements")[0].GetString(), "requirement");
                    }
                }),
                Check.Async(S, "JSON-11", "Output validates against the published schema", async ct =>
                {
                    List<string> documents = new List<string>
                    {
                        await json(await MixedRun(ct)),
                        await json(await Run(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A")) }, null, ct)),
                        await json(await Run(Array.Empty<TestSuiteDescriptor>(), null, ct)),
                    };
                    foreach (string document in documents)
                    {
                        IReadOnlyList<string> errors = SchemaValidator.Validate(JsonReport.Schema, document);
                        TestAssert.Equal(string.Empty, string.Join("; ", errors), "schema violations");
                    }

                    IReadOnlyList<string> broken = SchemaValidator.Validate(JsonReport.Schema, "{\"schemaVersion\":\"2.0\",\"tool\":{},\"run\":{},\"results\":[{\"outcome\":\"weird\"}]}");
                    TestAssert.True(broken.Count > 3, "validator detects violations");
                }),
                Check.Sync(S, "JSON-12", "The schema is embedded and matches schema version 1.0", () =>
                {
                    TestAssert.Contains("\"const\": \"1.0\"", JsonReport.Schema, "schema version");
                    TestAssert.Equal("1.0", JsonReport.SchemaVersion, "constant");
                }),
                Check.Async(S, "JSON-13", "Export works without runtime reflection (this case also runs in Test.Aot)", async ct =>
                {
                    string text = await json(await Run(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A")) }, null, ct));
                    TestAssert.Equal(string.Empty, string.Join("; ", SchemaValidator.Validate(JsonReport.Schema, text)), "valid document");
                }),
                Check.Async(S, "JSON-14", "Exception objects are never serialized", async ct =>
                {
                    string text = await json(await MixedRun(ct));
                    TestAssert.DoesNotContain("StackTrace", text, "stack trace");
                    TestAssert.DoesNotContain("System.InvalidOperationException", text, "exception type");
                }),
                Check.Async(S, "JSON-15", "Quotes, newlines, and non-ASCII text round-trip", async ct =>
                {
                    string tricky = "quote \" backslash \\ newline \n tab \t café 日本";
                    TestResult result = new TestResult("R", "A", tricky, TestOutcome.Failed, TimeSpan.Zero, tricky);
                    string text = await Export((p, o) => JsonReport.WriteAsync(new TestRunSummary(0, 1, 0, 0, 0, 0, TimeSpan.Zero), new[] { result }, p, o));
                    using (JsonDocument doc = JsonDocument.Parse(text))
                    {
                        TestAssert.Equal(tricky, doc.RootElement.GetProperty("results")[0].GetProperty("displayName").GetString(), "name");
                        TestAssert.Equal(tricky, doc.RootElement.GetProperty("results")[0].GetProperty("message").GetString(), "message");
                    }
                }),
            });
        }

        private static TestSuiteDescriptor HtmlSuite()
        {
            const string S = "Html";
            Func<RunCapture, ReportOptions?, Task<string>> html = (run, options) => Export((p, o) => HtmlReport.WriteAsync(run.Summary, run.Results, p, o), options);

            return new TestSuiteDescriptor(S, "HTML report", new[]
            {
                Check.Async(S, "HTML-01", "Hostile names and messages are escaped", async ct =>
                {
                    TestResult result = new TestResult("R", "A", "<script>alert(1)</script>", TestOutcome.Failed, TimeSpan.Zero, "<img src=x onerror=y>");
                    string text = await html(new RunCapture(new TestRunSummary(0, 1, 0, 0, 0, 0, TimeSpan.Zero), new[] { result }), new ReportOptions { RunId = "<run>" });
                    TestAssert.DoesNotContain("<script>alert", text, "script");
                    TestAssert.DoesNotContain("<img", text, "img");
                    TestAssert.Contains("&lt;script&gt;", text, "escaped");
                    TestAssert.Contains("&lt;run&gt;", text, "run id escaped");
                }),
                Check.Async(S, "HTML-02", "Redact applies to every consumer string", async ct =>
                {
                    string text = await html(await MixedRun(ct), new ReportOptions { Redact = s => s.Replace("fail", "*", StringComparison.OrdinalIgnoreCase).Replace("Feature", "*", StringComparison.Ordinal) });
                    TestAssert.DoesNotContain("teardown failed", text, "lifecycle");
                    TestAssert.DoesNotContain("step failed", text, "step");
                    TestAssert.DoesNotContain("Feature X", text, "feature");
                }),
                Check.Async(S, "HTML-03", "No remote resources or scripts are present", async ct =>
                {
                    string text = await html(await MixedRun(ct), null);
                    TestAssert.DoesNotContain("<script", text, "script");
                    TestAssert.DoesNotContain("http://", text, "http");
                    TestAssert.DoesNotContain("https://", text, "https");
                    TestAssert.DoesNotContain("<link", text, "link");
                }),
                Check.Async(S, "HTML-04", "Counts come from outcomes, including canceled and not-run", async ct =>
                {
                    RunCapture run = await MixedRun(ct);
                    string text = await html(run, null);
                    TestAssert.Contains("Canceled: " + run.Summary.Canceled + ".", text, "canceled");
                    TestAssert.Contains("Not run: " + run.Summary.NotRun + ".", text, "not run");
                    TestAssert.Contains("Filtered: 1.", text, "filtered");
                    TestAssert.Contains("Failed: " + run.Summary.Failed + ".", text, "failed");
                }),
                Check.Async(S, "HTML-05", "The run start time is shown, not the write time", async ct =>
                {
                    DateTimeOffset started = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
                    string text = await html(await Run(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A")) }, null, ct), new ReportOptions { StartedUtc = started });
                    TestAssert.Contains("Started: " + started.ToString("O", CultureInfo.InvariantCulture), text, "start time");
                }),
                Check.Async(S, "HTML-06", "Tags, feature grouping, examples, and requirements are rendered", async ct =>
                {
                    string text = await html(await MixedRun(ct), null);
                    TestAssert.Contains("Tags: fast", text, "tags");
                    TestAssert.Contains("<h2>Feature X</h2>", text, "feature group");
                    TestAssert.Contains("Example: ex-1", text, "example");
                    TestAssert.Contains("Requirements: REQ-1", text, "requirements");
                }),
                Check.Async(S, "HTML-07", "Cancellation stops writing", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        RunCapture run = await Run(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A")) }, null, ct);
                        string path = Path.Combine(Path.GetTempPath(), "touchstone-" + Guid.NewGuid().ToString("N"));
                        await TestAssert.ThrowsAsync<OperationCanceledException>(() => HtmlReport.WriteAsync(run.Summary, run.Results, path, null, source.Token), "html");
                        await TestAssert.ThrowsAsync<OperationCanceledException>(() => JsonReport.WriteAsync(run.Summary, run.Results, path, null, source.Token), "json");
                        TestAssert.False(File.Exists(path), "no file");
                    }
                }),
                Check.Async(S, "HTML-08", "Null and empty arguments are rejected", async ct =>
                {
                    TestRunSummary s = new TestRunSummary(0, 0, 0, 0, 0, 0, TimeSpan.Zero);
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => HtmlReport.WriteAsync(null!, Array.Empty<TestResult>(), "x", null, ct), "summary");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => HtmlReport.WriteAsync(s, null!, "x", null, ct), "results");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => HtmlReport.WriteAsync(s, Array.Empty<TestResult>(), null!, null, ct), "path");
                    await TestAssert.ThrowsAsync<ArgumentException>(() => HtmlReport.WriteAsync(s, Array.Empty<TestResult>(), string.Empty, null, ct), "empty path");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => JsonReport.WriteAsync(null!, Array.Empty<TestResult>(), "x", null, ct), "json summary");
                    TestAssert.Throws<ArgumentException>(() => new ReportOptions().RunId = " ", "blank run id");
                    TestAssert.Throws<ArgumentNullException>(() => new ReportOptions().Redact = null!, "null redact");
                }),
                Check.Async(S, "HTML-09", "Step tables have captions and column scopes", async ct =>
                {
                    string text = await html(await MixedRun(ct), null);
                    TestAssert.Contains("<caption>Steps</caption>", text, "caption");
                    TestAssert.Contains("<th scope=\"col\">Behavior</th>", text, "scope");
                    TestAssert.Contains("<html lang=\"en\">", text, "language");
                }),
            });
        }

        private static TestSuiteDescriptor DiagnosticsSuite()
        {
            const string S = "Diagnostics";
            return new TestSuiteDescriptor(S, "Diagnostics", new[]
            {
                Check.Sync(S, "DIAG-01", "Format lists every step with kind, name, outcome, duration, and message", () =>
                {
                    TestResult result = new TestResult("D", "A", "N", TestOutcome.Failed, TimeSpan.Zero, steps: new[]
                    {
                        new ScenarioStepResult("a", "first step", ScenarioStepKind.Given, ScenarioStepOutcome.Passed, TimeSpan.FromMilliseconds(1.5)),
                        new ScenarioStepResult("b", "second step", ScenarioStepKind.Then, ScenarioStepOutcome.Failed, TimeSpan.Zero, new InvalidOperationException("why")),
                    });
                    string text = ScenarioDiagnostics.Format(result);
                    TestAssert.Contains("GIVEN first step : Passed (1.5ms)", text, "first");
                    TestAssert.Contains("THEN second step : Failed (0.0ms) - why", text, "second");
                }),
                Check.Sync(S, "DIAG-02", "Format lists cleanup errors", () =>
                {
                    TestResult result = new TestResult("D", "A", "N", TestOutcome.Failed, TimeSpan.Zero, cleanupExceptions: new Exception[] { new InvalidOperationException("dispose broke") });
                    TestAssert.Contains("Cleanup: dispose broke", ScenarioDiagnostics.Format(result), "cleanup");
                }),
                Check.Sync(S, "DIAG-03", "Format of an ordinary case is empty", () =>
                {
                    TestAssert.Equal(string.Empty, ScenarioDiagnostics.Format(new TestResult("D", "A", "N", TestOutcome.Failed, TimeSpan.Zero, "x")), "empty");
                    TestAssert.Throws<ArgumentNullException>(() => ScenarioDiagnostics.Format(null!), "null");
                }),
                Check.Async(S, "DIAG-04", "ExecuteAsync sends diagnostics and rethrows the original exception", async ct =>
                {
                    string output = string.Empty;
                    FormatException error = new FormatException("bad");
                    TestCaseDescriptor scenario = Fixtures.Scenario("D", "Scn", (c, t) => throw error);
                    FormatException thrown = await TestAssert.ThrowsAsync<FormatException>(() => ScenarioDiagnostics.ExecuteAsync(scenario, text => output = text, null, ct), "rethrow");
                    TestAssert.Same(error, thrown, "same");
                    TestAssert.Contains("the action runs", output, "diagnostics");
                }),
                Check.Async(S, "DIAG-05", "ExecuteAsync does not call output for an ordinary case", async ct =>
                {
                    bool called = false;
                    await ScenarioDiagnostics.ExecuteAsync(Fixtures.Passing("D", "A"), _ => called = true, null, ct);
                    TestAssert.False(called, "output");
                    await TestAssert.ThrowsAsync<ArgumentNullException>(() => ScenarioDiagnostics.ExecuteAsync(Fixtures.Passing("D", "A"), null!, null, ct), "null output");
                }),
                Check.Async(S, "DIAG-06", "The shared failure formatter reports identities, messages, diagnostics, and original exceptions", async ct =>
                {
                    TestResultCollector collector = new TestResultCollector();
                    InvalidOperationException setup = new InvalidOperationException("setup broke");
                    TestRunSummary summary = await TestExecutor.RunAsync(new[]
                    {
                        Fixtures.Suite("D", Fixtures.Failing("D", "A", "first problem"), Fixtures.Scenario("D", "Scn", (c, t) => throw new FormatException("second problem"))),
                        Fixtures.HookedSuite("E", null, _ => throw setup, Fixtures.Passing("E", "B")),
                    }, collector, null, ct);
                    AggregateException ex = TestAssert.Throws<AggregateException>(() => TestRunAssert.ThrowIfUnsuccessful(summary, collector.Results, ct), "throws");
                    TestAssert.Contains("D.A (Failed): first problem", ex.Message, "first");
                    TestAssert.Contains("WHEN the action runs : Failed", ex.Message, "diagnostics");
                    TestAssert.Contains("E after_suite: setup broke", ex.Message, "lifecycle");
                    TestAssert.Equal(3, ex.InnerExceptions.Count, "inner exceptions");
                    TestAssert.True(ex.InnerExceptions.Contains(setup), "hook exception");
                    TestAssert.Null(TestRunAssert.CreateFailure(new TestRunSummary(1, 0, 0, 0, 0, 0, TimeSpan.Zero), Array.Empty<TestResult>()), "success returns null");
                    TestAssert.Throws<OperationCanceledException>(() => TestRunAssert.ThrowIfUnsuccessful(new TestRunSummary(0, 0, 0, 0, 1, 0, TimeSpan.Zero, true), Array.Empty<TestResult>(), ct), "canceled");
                }),
                Check.Sync(S, "DIAG-07", "Numbers use the invariant culture", () =>
                {
                    CultureInfo previous = CultureInfo.CurrentCulture;
                    try
                    {
                        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                        TestResult result = new TestResult("D", "A", "N", TestOutcome.Passed, TimeSpan.Zero, steps: new[]
                        {
                            new ScenarioStepResult("a", "step", ScenarioStepKind.Given, ScenarioStepOutcome.Passed, TimeSpan.FromMilliseconds(1.5)),
                        });
                        TestAssert.Contains("(1.5ms)", ScenarioDiagnostics.Format(result), "decimal point");
                    }
                    catch (CultureNotFoundException)
                    {
                        // Invariant-globalization hosts (Test.Aot) have no de-DE data; formatting is invariant there by definition.
                    }
                    finally
                    {
                        CultureInfo.CurrentCulture = previous;
                    }
                }),
            });
        }

        /// <summary>
        /// A completed run's summary and results.
        /// </summary>
        private sealed class RunCapture
        {
            internal RunCapture(TestRunSummary summary, IReadOnlyList<TestResult> results)
            {
                Summary = summary;
                Results = results;
            }

            internal TestRunSummary Summary { get; }

            internal IReadOnlyList<TestResult> Results { get; }
        }
    }
}
