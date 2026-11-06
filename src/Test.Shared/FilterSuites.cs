namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.6 and 11.9: filtering and skip behavior.
    /// </summary>
    public static class FilterSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return FilterSuite();
            yield return SkipSuite();
        }

        private static TestCaseDescriptor Tagged(string caseId, params string[] tags)
        {
            return Fixtures.Passing("F", caseId, null, tags);
        }

        private static TestRunOptions Options(Action<TestFilter> configure, SkipBehavior skip = SkipBehavior.Report)
        {
            TestRunOptions options = new TestRunOptions { SkipBehavior = skip };
            configure(options.Filter);
            return options;
        }

        private static async Task<string> RunIds(TestSuiteDescriptor suite, TestRunOptions options, CancellationToken ct)
        {
            RecordingSink sink = new RecordingSink();
            await TestExecutor.RunSuiteAsync(suite, sink, options, ct);
            return string.Join(",", sink.Results.Select(r => r.CaseId));
        }

        private static TestSuiteDescriptor FilterSuite()
        {
            const string S = "Filter";
            TestSuiteDescriptor sample = Fixtures.Suite("F", Tagged("A", "fast"), Tagged("B", "slow"), Tagged("C", "fast", "db"), Tagged("D"));
            TestSuiteDescriptor other = Fixtures.Suite("G", Fixtures.Passing("G", "A", null, new[] { "fast" }));

            return new TestSuiteDescriptor(S, "Filtering", new[]
            {
                Check.Async(S, "FLT-01", "An empty filter selects every case", async ct =>
                {
                    TestAssert.Equal("A,B,C,D", await RunIds(sample, new TestRunOptions(), ct), "ids");
                }),
                Check.Async(S, "FLT-02", "An include tag selects cases with that tag", async ct =>
                {
                    TestAssert.Equal("B", await RunIds(sample, Options(f => f.IncludeTags.Add("slow")), ct), "ids");
                }),
                Check.Async(S, "FLT-03", "Several include tags select cases with any of them", async ct =>
                {
                    TestAssert.Equal("B,C", await RunIds(sample, Options(f => { f.IncludeTags.Add("slow"); f.IncludeTags.Add("db"); }), ct), "ids");
                }),
                Check.Async(S, "FLT-04", "An exclude tag removes cases with that tag", async ct =>
                {
                    TestAssert.Equal("B,D", await RunIds(sample, Options(f => f.ExcludeTags.Add("fast")), ct), "ids");
                }),
                Check.Async(S, "FLT-05", "Exclusion wins over inclusion", async ct =>
                {
                    TestAssert.Equal("A", await RunIds(sample, Options(f => { f.IncludeTags.Add("fast"); f.ExcludeTags.Add("db"); }), ct), "ids");
                }),
                Check.Async(S, "FLT-06", "Tag matching is case-insensitive", async ct =>
                {
                    TestAssert.Equal("A,C", await RunIds(sample, Options(f => f.IncludeTags.Add("FAST")), ct), "include");
                    TestAssert.Equal("B,D", await RunIds(sample, Options(f => f.ExcludeTags.Add("Fast")), ct), "exclude");
                }),
                Check.Sync(S, "FLT-07", "A suite filter selects only that suite, ordinally", () =>
                {
                    TestFilter filter = new TestFilter();
                    filter.SuiteIds.Add("G");
                    TestAssert.False(filter.Matches(sample.Cases[0]), "other suite");
                    TestAssert.True(filter.Matches(other.Cases[0]), "selected suite");
                    TestFilter lower = new TestFilter();
                    lower.SuiteIds.Add("g");
                    TestAssert.False(lower.Matches(other.Cases[0]), "ordinal comparison");
                }),
                Check.Async(S, "FLT-08", "A test identity filter selects only that case", async ct =>
                {
                    TestAssert.Equal("C", await RunIds(sample, Options(f => f.TestIds.Add("F.C")), ct), "ids");
                }),
                Check.Async(S, "FLT-09", "Suite, test, and tag criteria combine with AND", async ct =>
                {
                    TestAssert.Equal("C", await RunIds(sample, Options(f => { f.SuiteIds.Add("F"); f.IncludeTags.Add("fast"); f.TestIds.Add("F.C"); f.TestIds.Add("F.B"); }), ct), "ids");
                }),
                Check.Async(S, "FLT-10", "Untagged cases fail an include filter and pass an exclude-only filter", async ct =>
                {
                    TestAssert.DoesNotContain("D", await RunIds(sample, Options(f => f.IncludeTags.Add("fast")), ct), "include");
                    TestAssert.Contains("D", await RunIds(sample, Options(f => f.ExcludeTags.Add("fast")), ct), "exclude only");
                }),
                Check.Async(S, "FLT-11", "Filtered cases do not run, report, or count in Total", async ct =>
                {
                    bool ran = false;
                    TestSuiteDescriptor suite = Fixtures.Suite("F", Fixtures.Passing("F", "A", _ => ran = true), Tagged("B", "keep"));
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, sink, Options(f => f.IncludeTags.Add("keep")), ct);
                    TestAssert.False(ran, "filtered body");
                    TestAssert.False(sink.Events.Any(e => e.Contains("F.A")), "no callbacks for filtered case");
                    TestAssert.Equal(1, summary.Total, "total");
                    TestAssert.Equal(1, summary.Filtered, "filtered");
                }),
                Check.Async(S, "FLT-12", "A suite with no selected cases runs no hooks and no callbacks", async ct =>
                {
                    int hooks = 0;
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("F", _ => { hooks++; return default; }, _ => { hooks++; return default; }, Tagged("A"));
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, sink, Options(f => f.IncludeTags.Add("none")), ct);
                    TestAssert.Equal(0, hooks, "hooks");
                    TestAssert.Equal(0, sink.Events.Count, "callbacks");
                    TestAssert.Equal(1, summary.Filtered, "filtered");
                }),
                Check.Async(S, "FLT-13", "A filter that selects nothing returns a successful empty summary", async ct =>
                {
                    TestRunSummary summary = await TestExecutor.RunAsync(new[] { sample, other }, null, Options(f => f.IncludeTags.Add("none")), ct);
                    TestAssert.True(summary.IsSuccess, "success");
                    TestAssert.Equal(0, summary.Total, "total");
                    TestAssert.Equal(5, summary.Filtered, "filtered");
                }),
                Check.Async(S, "FLT-14", "Scenario tags filter like ordinary tags", async ct =>
                {
                    TestCaseDescriptor scenario = new ScenarioBuilder<ScenarioContext>("F", "Scn", "Scenario", _ => Task.FromResult(new ScenarioContext()))
                        .WithTags(new[] { "bdd" })
                        .Then("ok", "it works", (c, t) => Task.CompletedTask)
                        .Build();
                    TestSuiteDescriptor suite = Fixtures.Suite("F", scenario, Tagged("A"));
                    TestAssert.Equal("bdd", scenario.Tags.Single(), "tag");
                    TestAssert.Equal("Scn", await RunIds(suite, Options(f => f.IncludeTags.Add("BDD")), ct), "ids");
                }),
                Check.Sync(S, "FLT-15", "Matches rejects a null case", () =>
                {
                    TestAssert.Throws<ArgumentNullException>(() => new TestFilter().Matches(null!), "null");
                }),
                Check.Async(S, "FLT-16", "Invalid filter values fail when the run starts, naming the value", async ct =>
                {
                    TestRunOptions badTag = Options(f => f.IncludeTags.Add("bad tag"));
                    ArgumentException ex = await TestAssert.ThrowsAsync<ArgumentException>(() => TestExecutor.RunSuiteAsync(sample, null, badTag, ct), "bad tag");
                    TestAssert.Contains("bad tag", ex.Message, "names value");
                    await TestAssert.ThrowsAsync<ArgumentException>(() => TestExecutor.RunSuiteAsync(sample, null, Options(f => f.SuiteIds.Add("a.b")), ct), "bad suite");
                    await TestAssert.ThrowsAsync<ArgumentException>(() => TestExecutor.RunSuiteAsync(sample, null, Options(f => f.TestIds.Add(" ")), ct), "blank test id");
                    await TestAssert.ThrowsAsync<ArgumentException>(() => TestExecutor.RunSuiteAsync(sample, null, Options(f => f.ExcludeTags.Add(null!)), ct), "null tag");
                    TestAssert.Throws<ArgumentException>(() => badTag.Filter.Validate(), "Validate");
                }),
                Check.Async(S, "FLT-17", "Filtered cases emit no case span and increment touchstone.cases.filtered", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.RunSuiteAsync(sample, null, Options(f => f.IncludeTags.Add("slow")), ct);
                        TestAssert.Equal(1, capture.Spans().Count(a => a.DisplayName == TouchstoneTelemetry.CaseSpanName), "case spans");
                        TestAssert.Equal(3.0, capture.Measurements(TouchstoneTelemetry.CasesFiltered).Sum(m => m.Value), "filtered count");
                    }
                }),
                Check.Async(S, "FLT-18", "Executing an excluded case returns NotRun with a filtered message", async ct =>
                {
                    bool ran = false;
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(Fixtures.Passing("F", "A", _ => ran = true), null, Options(f => f.IncludeTags.Add("x")), ct);
                    TestAssert.Equal(TestOutcome.NotRun, result.Outcome, "outcome");
                    TestAssert.Contains("filter", result.Message, "message");
                    TestAssert.False(ran, "body");
                    await TestExecutor.ExecuteCaseAsync(Fixtures.Failing("F", "B"), Options(f => f.IncludeTags.Add("x")), ct);
                }),
            });
        }

        private static TestSuiteDescriptor SkipSuite()
        {
            const string S = "Skip";
            Func<StrongBox<bool>, TestCaseDescriptor> skippedScenario = ran =>
                new ScenarioBuilder<ScenarioContext>("K", "Scn", "Skipped scenario", _ =>
                {
                    ran.Value = true;
                    return Task.FromResult(new ScenarioContext());
                })
                .WithSkip("later")
                .Given("a", "a", (c, t) => Task.CompletedTask)
                .Then("b", "b", (c, t) => Task.CompletedTask)
                .Build();

            return new TestSuiteDescriptor(S, "Skip behavior", new[]
            {
                Check.Async(S, "SKP-01", "Report: skipped cases do not run and report Skipped with the reason", async ct =>
                {
                    StrongBox<bool> ran = new StrongBox<bool>();
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("K", Fixtures.Skipped("K", "A", ran, "blocked")), sink, null, ct);
                    TestAssert.False(ran.Value, "ran");
                    TestAssert.Equal(TestOutcome.Skipped, sink.Results[0].Outcome, "outcome");
                    TestAssert.Equal("blocked", sink.Results[0].Message, "message");
                    TestAssert.Equal(1, summary.Skipped, "count");
                }),
                Check.Async(S, "SKP-02", "Report: skipped scenarios create no context and report Skipped steps", async ct =>
                {
                    StrongBox<bool> ran = new StrongBox<bool>();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(skippedScenario(ran), null, null, ct);
                    TestAssert.False(ran.Value, "context created");
                    TestAssert.Equal(TestOutcome.Skipped, result.Outcome, "outcome");
                    TestAssert.True(result.Steps.All(s => s.Outcome == ScenarioStepOutcome.Skipped), "steps skipped");
                    TestAssert.Equal(2, result.Steps.Count, "step count");
                }),
                Check.Async(S, "SKP-03", "Omit: skipped cases are not run, reported, or counted, and count as filtered", async ct =>
                {
                    StrongBox<bool> ran = new StrongBox<bool>();
                    RecordingSink sink = new RecordingSink();
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("K", Fixtures.Skipped("K", "A", ran), Fixtures.Passing("K", "B")), sink,
                        new TestRunOptions { SkipBehavior = SkipBehavior.Omit }, ct);
                    TestAssert.False(ran.Value, "ran");
                    TestAssert.Equal("B", string.Join(",", sink.Results.Select(r => r.CaseId)), "reported");
                    TestAssert.Equal(1, summary.Total, "total");
                    TestAssert.Equal(1, summary.Filtered, "filtered");
                }),
                Check.Async(S, "SKP-04", "Omit: a suite of only skipped cases runs no hooks", async ct =>
                {
                    int hooks = 0;
                    TestSuiteDescriptor suite = Fixtures.HookedSuite("K", _ => { hooks++; return default; }, _ => { hooks++; return default; }, Fixtures.Skipped("K", "A"));
                    await TestExecutor.RunSuiteAsync(suite, null, new TestRunOptions { SkipBehavior = SkipBehavior.Omit }, ct);
                    TestAssert.Equal(0, hooks, "hooks");
                }),
                Check.Async(S, "SKP-05", "Fail: skipped cases do not run and fail with SkippedCaseNotAllowedException", async ct =>
                {
                    foreach (bool scenario in new[] { false, true })
                    {
                        StrongBox<bool> ran = new StrongBox<bool>();
                        TestCaseDescriptor testCase = scenario ? skippedScenario(ran) : Fixtures.Skipped("K", "A", ran, "later");
                        TestResult result = await TestExecutor.ExecuteCaseResultAsync(testCase, null, new TestRunOptions { SkipBehavior = SkipBehavior.Fail }, ct);
                        TestAssert.False(ran.Value, "ran (scenario=" + scenario + ")");
                        TestAssert.Equal(TestOutcome.Failed, result.Outcome, "outcome");
                        SkippedCaseNotAllowedException error = (SkippedCaseNotAllowedException)result.Exception!;
                        TestAssert.Equal("later", error.SkipReason, "reason");
                        TestAssert.Equal(testCase.TestId, error.TestId, "test id");
                        TestAssert.Contains("later", result.Message, "message");
                    }
                }),
                Check.Async(S, "SKP-06", "Fail: the run is unsuccessful", async ct =>
                {
                    TestRunSummary summary = await TestExecutor.RunAsync(new[] { Fixtures.Suite("K", Fixtures.Skipped("K", "A")) }, null,
                        new TestRunOptions { SkipBehavior = SkipBehavior.Fail }, ct);
                    TestAssert.False(summary.IsSuccess, "success");
                    TestAssert.Equal(1, summary.Failed, "failed");
                }),
                Check.Async(S, "SKP-07", "ExecuteCaseAsync returns normally for a skipped case under Report", async ct =>
                {
                    await TestExecutor.ExecuteCaseAsync(Fixtures.Skipped("K", "A"), null, ct);
                }),
                Check.Async(S, "SKP-08", "ExecuteCaseAsync throws SkippedCaseNotAllowedException under Fail", async ct =>
                {
                    await TestAssert.ThrowsAsync<SkippedCaseNotAllowedException>(
                        () => TestExecutor.ExecuteCaseAsync(Fixtures.Skipped("K", "A"), new TestRunOptions { SkipBehavior = SkipBehavior.Fail }, ct), "fail");
                }),
                Check.Async(S, "SKP-09", "ExecuteCaseResultAsync under Omit returns NotRun with an omitted message", async ct =>
                {
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(Fixtures.Skipped("K", "A"), null, new TestRunOptions { SkipBehavior = SkipBehavior.Omit }, ct);
                    TestAssert.Equal(TestOutcome.NotRun, result.Outcome, "outcome");
                    TestAssert.Contains("Omit", result.Message, "message");
                }),
                Check.Async(S, "SKP-10", "A skipped case records a skipped span and never becomes active", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestExecutor.ExecuteCaseResultAsync(Fixtures.Skipped("K", "A"), null, null, ct);
                        TestAssert.Equal("skipped", capture.Span(TouchstoneTelemetry.CaseSpanName).GetTagItem(TouchstoneTelemetry.AttributeOutcome)?.ToString(), "span outcome");
                        TestAssert.Equal(0, capture.Measurements(TouchstoneTelemetry.CasesActive).Count, "active");
                    }
                }),
                Check.Async(S, "SKP-11", "The filter applies before skip behavior", async ct =>
                {
                    TestRunOptions options = new TestRunOptions { SkipBehavior = SkipBehavior.Fail };
                    options.Filter.IncludeTags.Add("keep");
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(Fixtures.Suite("K", Fixtures.Skipped("K", "A"), Fixtures.Passing("K", "B", null, new[] { "keep" })), null, options, ct);
                    TestAssert.Equal(1, summary.Filtered, "filtered once");
                    TestAssert.Equal(0, summary.Failed, "not failed");
                    TestAssert.True(summary.IsSuccess, "success");
                }),
            });
        }
    }
}
