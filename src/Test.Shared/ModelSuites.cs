namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.5 and 11.7: results, summaries, lifecycle errors, and run options.
    /// </summary>
    public static class ModelSuites
    {
        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return ResultSuite();
            yield return SummarySuite();
            yield return OptionsSuite();
        }

        private static TestSuiteDescriptor ResultSuite()
        {
            const string S = "Result";
            return new TestSuiteDescriptor(S, "Results and lifecycle errors", new[]
            {
                Check.Sync(S, "RES-01", "Constructor stores every value", () =>
                {
                    InvalidOperationException error = new InvalidOperationException("x");
                    ScenarioMetadata metadata = new ScenarioMetadata("feature");
                    ScenarioStepResult step = new ScenarioStepResult("s", "step", ScenarioStepKind.Then, ScenarioStepOutcome.Failed, TimeSpan.FromMilliseconds(2));
                    TestResult r = new TestResult("A", "B", "Name", TestOutcome.Failed, TimeSpan.FromSeconds(1), "msg", error,
                        new[] { "t" }, metadata, new[] { step }, new Exception[] { error });
                    TestAssert.Equal("A", r.SuiteId, "suite");
                    TestAssert.Equal("B", r.CaseId, "case");
                    TestAssert.Equal("Name", r.DisplayName, "name");
                    TestAssert.Equal(TestOutcome.Failed, r.Outcome, "outcome");
                    TestAssert.Equal(TimeSpan.FromSeconds(1), r.Duration, "duration");
                    TestAssert.Equal("msg", r.Message, "message");
                    TestAssert.Same(error, r.Exception, "exception");
                    TestAssert.Equal("t", r.Tags[0], "tag");
                    TestAssert.Same(metadata, r.Metadata, "metadata");
                    TestAssert.Same(step, r.Steps[0], "step");
                    TestAssert.Same(error, r.CleanupExceptions[0], "cleanup");
                    TestAssert.Equal("A.B: Failed", r.ToString(), "to string");
                }),
                Check.Sync(S, "RES-02", "Null identities throw ArgumentNullException", () =>
                {
                    TestAssert.Throws<ArgumentNullException>(() => new TestResult(null!, "B", "N", TestOutcome.Passed, TimeSpan.Zero), "suite");
                    TestAssert.Throws<ArgumentNullException>(() => new TestResult("A", null!, "N", TestOutcome.Passed, TimeSpan.Zero), "case");
                    TestAssert.Throws<ArgumentNullException>(() => new TestResult("A", "B", null!, TestOutcome.Passed, TimeSpan.Zero), "name");
                }),
                Check.Sync(S, "RES-03", "A negative duration is rejected", () =>
                {
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestResult("A", "B", "N", TestOutcome.Passed, TimeSpan.FromTicks(-1)), "duration");
                }),
                Check.Sync(S, "RES-04", "An undefined outcome is rejected", () =>
                {
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestResult("A", "B", "N", (TestOutcome)42, TimeSpan.Zero), "outcome");
                }),
                Check.Sync(S, "RES-05", "Optional collections default to empty", () =>
                {
                    TestResult r = new TestResult("A", "B", "N", TestOutcome.Passed, TimeSpan.Zero);
                    TestAssert.Equal(0, r.Tags.Count, "tags");
                    TestAssert.Equal(0, r.Steps.Count, "steps");
                    TestAssert.Equal(0, r.CleanupExceptions.Count, "cleanup");
                    TestAssert.Null(r.Metadata, "metadata");
                    TestAssert.Null(r.Exception, "exception");
                    TestAssert.Null(r.Message, "message");
                }),
                Check.Sync(S, "RES-06", "Collections are copied and null entries rejected", () =>
                {
                    List<string> tags = new List<string> { "a" };
                    TestResult r = new TestResult("A", "B", "N", TestOutcome.Passed, TimeSpan.Zero, tags: tags);
                    tags.Add("b");
                    TestAssert.Equal(1, r.Tags.Count, "copied");
                    TestAssert.Throws<ArgumentNullException>(() => new TestResult("A", "B", "N", TestOutcome.Passed, TimeSpan.Zero, tags: new string[] { null! }), "null tag");
                }),
                Check.Sync(S, "RES-07", "TestId is composed from suite and case identities", () =>
                {
                    TestAssert.Equal("A.B", new TestResult("A", "B", "N", TestOutcome.Passed, TimeSpan.Zero).TestId, "test id");
                }),
                Check.Sync(S, "LCE-01", "SuiteLifecycleError stores values and rejects nulls", () =>
                {
                    Exception error = new InvalidOperationException("hook");
                    SuiteLifecycleError e = new SuiteLifecycleError("X", TouchstoneTelemetry.StageBeforeSuite, error);
                    TestAssert.Equal("X", e.SuiteId, "suite");
                    TestAssert.Equal("before_suite", e.Stage, "stage");
                    TestAssert.Same(error, e.Exception, "exception");
                    TestAssert.Equal("X before_suite: hook", e.ToString(), "to string");
                    TestAssert.Throws<ArgumentNullException>(() => new SuiteLifecycleError(null!, "before_suite", error), "suite");
                    TestAssert.Throws<ArgumentNullException>(() => new SuiteLifecycleError("X", null!, error), "stage");
                    TestAssert.Throws<ArgumentNullException>(() => new SuiteLifecycleError("X", "before_suite", null!), "exception");
                }),
                Check.Sync(S, "LCE-02", "Stage is restricted to before_suite and after_suite", () =>
                {
                    TestAssert.Equal("after_suite", new SuiteLifecycleError("X", "after_suite", new Exception("x")).Stage, "after");
                    TestAssert.Throws<ArgumentException>(() => new SuiteLifecycleError("X", "case", new Exception("x")), "other stage");
                }),
            });
        }

        private static TestSuiteDescriptor SummarySuite()
        {
            const string S = "Summary";
            SuiteLifecycleError lifecycle = new SuiteLifecycleError("X", "after_suite", new InvalidOperationException("x"));
            return new TestSuiteDescriptor(S, "Run summary", new[]
            {
                Check.Sync(S, "SUM-01", "Total is the sum of the five outcome counts", () =>
                {
                    TestRunSummary s = new TestRunSummary(1, 2, 3, 4, 5, 6, TimeSpan.FromSeconds(1));
                    TestAssert.Equal(15, s.Total, "total");
                    TestAssert.Equal(1, s.Passed, "passed");
                    TestAssert.Equal(2, s.Failed, "failed");
                    TestAssert.Equal(3, s.Skipped, "skipped");
                    TestAssert.Equal(4, s.Canceled, "canceled");
                    TestAssert.Equal(5, s.NotRun, "not run");
                    TestAssert.Equal(TimeSpan.FromSeconds(1), s.Duration, "duration");
                }),
                Check.Sync(S, "SUM-02", "Filtered cases are not part of Total", () =>
                {
                    TestRunSummary s = new TestRunSummary(1, 0, 0, 0, 0, 9, TimeSpan.Zero);
                    TestAssert.Equal(1, s.Total, "total");
                    TestAssert.Equal(9, s.Filtered, "filtered");
                }),
                Check.Sync(S, "SUM-03", "Negative counts and durations are rejected", () =>
                {
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(-1, 0, 0, 0, 0, 0, TimeSpan.Zero), "passed");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(0, -1, 0, 0, 0, 0, TimeSpan.Zero), "failed");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(0, 0, -1, 0, 0, 0, TimeSpan.Zero), "skipped");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(0, 0, 0, -1, 0, 0, TimeSpan.Zero), "canceled");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(0, 0, 0, 0, -1, 0, TimeSpan.Zero), "not run");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(0, 0, 0, 0, 0, -1, TimeSpan.Zero), "filtered");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunSummary(0, 0, 0, 0, 0, 0, TimeSpan.FromTicks(-1)), "duration");
                }),
                Check.Sync(S, "SUM-04", "IsSuccess holds for passed-only, skipped-only, and empty runs", () =>
                {
                    TestAssert.True(new TestRunSummary(3, 0, 0, 0, 0, 0, TimeSpan.Zero).IsSuccess, "passed");
                    TestAssert.True(new TestRunSummary(0, 0, 3, 0, 0, 0, TimeSpan.Zero).IsSuccess, "skipped");
                    TestAssert.True(new TestRunSummary(0, 0, 0, 0, 0, 0, TimeSpan.Zero).IsSuccess, "empty");
                    TestAssert.True(new TestRunSummary(0, 0, 0, 0, 0, 5, TimeSpan.Zero).IsSuccess, "filtered only");
                }),
                Check.Sync(S, "SUM-05", "IsSuccess is false for failed, canceled, or not-run cases", () =>
                {
                    TestAssert.False(new TestRunSummary(1, 1, 0, 0, 0, 0, TimeSpan.Zero).IsSuccess, "failed");
                    TestAssert.False(new TestRunSummary(1, 0, 0, 1, 0, 0, TimeSpan.Zero).IsSuccess, "canceled");
                    TestAssert.False(new TestRunSummary(1, 0, 0, 0, 1, 0, TimeSpan.Zero).IsSuccess, "not run");
                }),
                Check.Sync(S, "SUM-06", "A lifecycle error makes the summary unsuccessful", () =>
                {
                    TestRunSummary s = new TestRunSummary(1, 0, 0, 0, 0, 0, TimeSpan.Zero, false, new[] { lifecycle });
                    TestAssert.False(s.IsSuccess, "success");
                    TestAssert.Same(lifecycle, s.LifecycleErrors[0], "error kept");
                    TestAssert.Throws<ArgumentNullException>(() => new TestRunSummary(0, 0, 0, 0, 0, 0, TimeSpan.Zero, false, new SuiteLifecycleError[] { null! }), "null error");
                }),
                Check.Sync(S, "SUM-07", "WasCanceled is stored independently of the canceled count", () =>
                {
                    TestRunSummary s = new TestRunSummary(0, 0, 0, 0, 2, 0, TimeSpan.Zero, true);
                    TestAssert.True(s.WasCanceled, "was canceled");
                    TestAssert.Equal(0, s.Canceled, "canceled count");
                    TestAssert.False(s.IsSuccess, "success");
                    TestAssert.False(new TestRunSummary(0, 0, 0, 0, 0, 0, TimeSpan.Zero, true).IsSuccess, "canceled empty run");
                }),
            });
        }

        private static TestSuiteDescriptor OptionsSuite()
        {
            const string S = "Options";
            return new TestSuiteDescriptor(S, "Run options", new[]
            {
                Check.Sync(S, "OPT-01", "Defaults", () =>
                {
                    TestRunOptions o = new TestRunOptions();
                    TestAssert.True(o.Filter.IsEmpty, "filter empty");
                    TestAssert.Equal(SkipBehavior.Report, o.SkipBehavior, "skip");
                    TestAssert.False(o.StopOnFirstFailure, "stop");
                    TestAssert.Equal(TimeSpan.FromSeconds(30), o.CleanupTimeout, "cleanup");
                }),
                Check.Sync(S, "OPT-02", "CleanupTimeout range is (0, 1 day]", () =>
                {
                    TestRunOptions o = new TestRunOptions();
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => o.CleanupTimeout = TimeSpan.Zero, "zero");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => o.CleanupTimeout = TimeSpan.FromSeconds(-1), "negative");
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => o.CleanupTimeout = TimeSpan.FromDays(1).Add(TimeSpan.FromTicks(1)), "over a day");
                    o.CleanupTimeout = TimeSpan.FromDays(1);
                    TestAssert.Equal(TimeSpan.FromDays(1), o.CleanupTimeout, "one day accepted");
                    o.CleanupTimeout = TimeSpan.FromTicks(1);
                    TestAssert.Equal(TimeSpan.FromTicks(1), o.CleanupTimeout, "minimum accepted");
                }),
                Check.Sync(S, "OPT-03", "A null filter is rejected", () =>
                {
                    TestAssert.Throws<ArgumentNullException>(() => new TestRunOptions().Filter = null!, "filter");
                }),
                Check.Sync(S, "OPT-04", "An undefined skip behavior is rejected", () =>
                {
                    TestAssert.Throws<ArgumentOutOfRangeException>(() => new TestRunOptions().SkipBehavior = (SkipBehavior)9, "skip");
                }),
                Check.Async(S, "OPT-05", "Null options behave like default options", async ct =>
                {
                    TestSuiteDescriptor suite = Fixtures.Suite("X", Fixtures.Passing("X", "A"), Fixtures.Skipped("X", "B"), Fixtures.Failing("X", "C"));
                    TestRunSummary withNull = await TestExecutor.RunSuiteAsync(suite, null, null, ct);
                    TestRunSummary withDefault = await TestExecutor.RunSuiteAsync(suite, null, new TestRunOptions(), ct);
                    TestAssert.Equal(withDefault.Passed + "/" + withDefault.Skipped + "/" + withDefault.Failed,
                        withNull.Passed + "/" + withNull.Skipped + "/" + withNull.Failed, "counts");
                }),
                Check.Async(S, "OPT-06", "Changing options during a run does not affect it", async ct =>
                {
                    TestRunOptions options = new TestRunOptions();
                    TestCaseDescriptor mutate = Fixtures.Passing("X", "A", _ =>
                    {
                        options.Filter.IncludeTags.Add("never");
                        options.SkipBehavior = SkipBehavior.Fail;
                        options.StopOnFirstFailure = true;
                    });
                    TestSuiteDescriptor suite = Fixtures.Suite("X", mutate, Fixtures.Failing("X", "B"), Fixtures.Passing("X", "C"), Fixtures.Skipped("X", "D"));
                    TestRunSummary summary = await TestExecutor.RunSuiteAsync(suite, null, options, ct);
                    TestAssert.Equal(2, summary.Passed, "passed");
                    TestAssert.Equal(1, summary.Failed, "failed");
                    TestAssert.Equal(1, summary.Skipped, "skipped");
                    TestAssert.Equal(0, summary.NotRun, "not run");
                }),
                Check.Async(S, "OPT-07", "One options instance drives sequential and concurrent runs", async ct =>
                {
                    TestRunOptions options = new TestRunOptions();
                    options.Filter.IncludeTags.Add("keep");
                    TestSuiteDescriptor suite = Fixtures.Suite("X", Fixtures.Passing("X", "A", null, new[] { "keep" }), Fixtures.Passing("X", "B"));
                    TestRunSummary first = await TestExecutor.RunSuiteAsync(suite, null, options, ct);
                    TestRunSummary[] concurrent = await Task.WhenAll(
                        TestExecutor.RunSuiteAsync(suite, null, options, ct),
                        TestExecutor.RunSuiteAsync(suite, null, options, ct));
                    TestAssert.Equal(1, first.Passed, "sequential");
                    TestAssert.Equal(1, concurrent[0].Passed, "concurrent 1");
                    TestAssert.Equal(1, concurrent[1].Filtered, "concurrent 2");
                }),
            });
        }
    }
}
