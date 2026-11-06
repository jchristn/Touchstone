namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog section 11.22 (ADP) plus the adapter rows of DUP-08, TEL-12, and CAN-22, generated for one adapter probe.
    /// Case identities carry the probe code, for example <c>ADP-09-X3</c>.
    /// </summary>
    public static class AdapterContractSuites
    {
        /// <summary>
        /// The adapter contract suite for one adapter.
        /// </summary>
        /// <param name="probe">Adapter probe.</param>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor Suite(IAdapterProbe probe)
        {
            string s = "Adapter" + probe.Code;
            string c = "-" + probe.Code;
            TestRunOptions omit = new TestRunOptions { SkipBehavior = SkipBehavior.Omit };
            TestRunOptions fail = new TestRunOptions { SkipBehavior = SkipBehavior.Fail };

            Func<string, AdapterRow> standard = caseId => probe.Rows(AdapterFixtureSet.Standard, null).Single(r => r.TestId == "AdpA." + caseId);

            return new TestSuiteDescriptor(s, "Adapter contract " + probe.Code, new[]
            {
                Check.Sync(s, "ADP-01" + c, "One row per selected case, across suites, in order", () =>
                {
                    TestAssert.Equal("AdpA.Pass,AdpA.Fail,AdpA.Skip,AdpA.Scn,AdpB.Other",
                        string.Join(",", probe.Rows(AdapterFixtureSet.Standard, null).Select(r => r.TestId)), "rows");
                }),
                Check.Sync(s, "ADP-02" + c, "No rows for empty suites and empty suite lists", () =>
                {
                    TestAssert.Equal(0, probe.Rows(AdapterFixtureSet.Empty, null).Count, "empty list");
                    TestAssert.Equal(0, probe.Rows(AdapterFixtureSet.EmptySuites, null).Count, "empty suite");
                }),
                Check.Sync(s, "ADP-03" + c, "A null suite source is rejected", () =>
                {
                    probe.ExpectNullSourceRejected();
                }),
                Check.Sync(s, "ADP-04" + c, "The source can be enumerated repeatedly with the same result", () =>
                {
                    string first = string.Join(",", probe.Rows(AdapterFixtureSet.Standard, null).Select(r => r.TestId + "/" + r.DisplayName));
                    string second = string.Join(",", probe.Rows(AdapterFixtureSet.Standard, null).Select(r => r.TestId + "/" + r.DisplayName));
                    TestAssert.Equal(first, second, "repeat");
                }),
                Check.Sync(s, "ADP-05" + c, "Each row's display name is the case display name", () =>
                {
                    TestAssert.Equal("Pass display,Fail display,Skip display,Scn display,Other display",
                        string.Join(",", probe.Rows(AdapterFixtureSet.Standard, null).Select(r => r.DisplayName)), "names");
                }),
                Check.Async(s, "ADP-06" + c, "Row keys survive the framework's round trip and still execute", async ct =>
                {
                    foreach (AdapterRow row in probe.Rows(AdapterFixtureSet.Standard, null))
                        TestAssert.Equal(row.TestId, probe.RoundTrip(row).TestId, "round trip " + row.TestId);
                    await probe.ExecuteAsync(probe.RoundTrip(standard("Pass")), null, ct);
                }),
                Check.Async(s, "ADP-07" + c, "An unknown key fails with a message naming the test identity", async ct =>
                {
                    AdapterRow? unknown = probe.Unknown();
                    if (unknown == null) return;
                    InvalidOperationException ex = await TestAssert.ThrowsAsync<InvalidOperationException>(() => probe.ExecuteAsync(unknown, null, ct), "unknown");
                    TestAssert.Contains(unknown.TestId, ex.Message, "names test identity");
                }),
                Check.Sync(s, "ADP-08" + c, "Tags become native traits or categories where supported", () =>
                {
                    string categories = string.Join(",", standard("Pass").Categories);
                    TestAssert.Equal(probe.RowTraits ? "fast,db" : string.Empty, categories, "categories");
                }),
                Check.Async(s, "ADP-09" + c, "Report: skipped cases use the native skip with their reason", async ct =>
                {
                    AdapterRow row = standard("Skip");
                    if (probe.NativeRowSkip)
                    {
                        TestAssert.Equal(AdapterFixtures.SkipReason, row.SkipReason, "row skip");
                        Exception? thrown = null;
                        try
                        {
                            await probe.ExecuteAsync(row, null, ct);
                        }
                        catch (Exception ex)
                        {
                            thrown = ex;
                        }

                        TestAssert.NotNull(thrown, "runtime skip signal");
                        TestAssert.Contains(AdapterFixtures.SkipReason, probe.NativeSkipReason(thrown!), "runtime skip reason");
                    }
                    else
                    {
                        TestAssert.Null(row.SkipReason, "no row skip");
                        string output = await probe.ExecuteAsync(row, null, ct);
                        TestAssert.Contains(AdapterFixtures.SkipReason, output, "reason written to output");
                    }
                }),
                Check.Sync(s, "ADP-10" + c, "Omit: skipped cases get no row", () =>
                {
                    TestAssert.False(probe.Rows(AdapterFixtureSet.Standard, omit).Any(r => r.TestId == "AdpA.Skip"), "skip row");
                    TestAssert.Equal(4, probe.Rows(AdapterFixtureSet.Standard, omit).Count, "row count");
                }),
                Check.Async(s, "ADP-11" + c, "Fail: the skipped row fails with SkippedCaseNotAllowedException", async ct =>
                {
                    AdapterRow row = probe.Rows(AdapterFixtureSet.Standard, fail).Single(r => r.TestId == "AdpA.Skip");
                    TestAssert.Null(row.SkipReason, "no row skip");
                    SkippedCaseNotAllowedException ex = await TestAssert.ThrowsAsync<SkippedCaseNotAllowedException>(() => probe.ExecuteAsync(row, fail, ct), "fail");
                    TestAssert.Equal(AdapterFixtures.SkipReason, ex.SkipReason, "reason");
                }),
                Check.Sync(s, "ADP-12" + c, "The source applies the filter", () =>
                {
                    TestRunOptions options = new TestRunOptions();
                    options.Filter.IncludeTags.Add("slow");
                    TestAssert.Equal("AdpB.Other", string.Join(",", probe.Rows(AdapterFixtureSet.Standard, options).Select(r => r.TestId)), "filtered rows");
                    TestRunOptions bad = new TestRunOptions();
                    bad.Filter.IncludeTags.Add("bad tag");
                    TestAssert.Throws<ArgumentException>(() => probe.Rows(AdapterFixtureSet.Standard, bad), "invalid filter");
                }),
                Check.Async(s, "ADP-13" + c, "The execute helper passes a passing case", async ct =>
                {
                    await probe.ExecuteAsync(standard("Pass"), null, ct);
                }),
                Check.Async(s, "ADP-14" + c, "The execute helper rethrows the original exception", async ct =>
                {
                    InvalidOperationException ex = await TestAssert.ThrowsAsync<InvalidOperationException>(() => probe.ExecuteAsync(standard("Fail"), null, ct), "failure");
                    TestAssert.Equal(AdapterFixtures.FailureMessage, ex.Message, "message");
                    TestAssert.Contains(nameof(Fixtures), ex.StackTrace, "original stack");
                }),
                Check.Async(s, "ADP-15" + c, "The execute helper throws OperationCanceledException on cancellation", async ct =>
                {
                    using (CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        AdapterRow row = probe.Rows(AdapterFixtureSet.Cancel, null).Single();
                        source.CancelAfter(50);
                        Exception? thrown = null;
                        try
                        {
                            await probe.ExecuteAsync(row, null, source.Token);
                        }
                        catch (Exception ex)
                        {
                            thrown = ex;
                        }

                        TestAssert.True(thrown is OperationCanceledException, "canceled: " + thrown?.GetType().Name);
                    }
                }),
                Check.Async(s, "ADP-16" + c, "The execute helper writes scenario diagnostics", async ct =>
                {
                    TestAssert.Contains(AdapterFixtures.StepName, await probe.ExecuteAsync(standard("Scn"), null, ct), "diagnostics");
                }),
                Check.Async(s, "ADP-17" + c, "RunAllAsync completes for passing suites and for no suites", async ct =>
                {
                    await probe.RunAllAsync(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A"), Fixtures.Passing("R", "B")) }, null, ct);
                    await probe.RunAllAsync(Array.Empty<TestSuiteDescriptor>(), null, ct);
                }),
                Check.Async(s, "ADP-18" + c, "RunAllAsync throws AggregateException with the original exceptions", async ct =>
                {
                    InvalidOperationException first = new InvalidOperationException("first");
                    FormatException second = new FormatException("second");
                    AggregateException ex = await TestAssert.ThrowsAsync<AggregateException>(() => probe.RunAllAsync(new[]
                    {
                        Fixtures.Suite("R", new TestCaseDescriptor("R", "A", "A", _ => throw first), Fixtures.Passing("R", "B"), new TestCaseDescriptor("R", "C", "C", _ => throw second)),
                    }, null, ct), "aggregate");
                    TestAssert.Equal(2, ex.InnerExceptions.Count, "inner count");
                    TestAssert.Same(first, ex.InnerExceptions[0], "first original");
                    TestAssert.Same(second, ex.InnerExceptions[1], "second original");
                    TestAssert.Contains("R.A", ex.Message, "lists failure");
                }),
                Check.Async(s, "ADP-19" + c, "RunAllAsync honors skip behavior", async ct =>
                {
                    StrongBox<bool> ran = new StrongBox<bool>();
                    TestSuiteDescriptor[] suites = { Fixtures.Suite("R", Fixtures.Skipped("R", "A", ran), Fixtures.Passing("R", "B")) };
                    await probe.RunAllAsync(suites, null, ct);
                    await probe.RunAllAsync(suites, omit, ct);
                    AggregateException ex = await TestAssert.ThrowsAsync<AggregateException>(() => probe.RunAllAsync(suites, fail, ct), "fail mode");
                    TestAssert.True(ex.InnerExceptions.Single() is SkippedCaseNotAllowedException, "skip failure");
                    TestAssert.False(ran.Value, "skipped body never ran");
                }),
                Check.Async(s, "ADP-20" + c, "RunAllAsync runs each suite's hooks around its own cases, including after failures", async ct =>
                {
                    List<string> order = new List<string>();
                    TestSuiteDescriptor[] suites =
                    {
                        Fixtures.HookedSuite("R", _ => { order.Add("R:before"); return default; }, _ => { order.Add("R:after"); return default; }, Fixtures.Failing("R", "A")),
                        Fixtures.HookedSuite("T", _ => { order.Add("T:before"); return default; }, _ => { order.Add("T:after"); return default; }, Fixtures.Passing("T", "B", _ => order.Add("T:B"))),
                    };
                    await TestAssert.ThrowsAsync<AggregateException>(() => probe.RunAllAsync(suites, null, ct), "failure");
                    TestAssert.Equal("R:before,R:after,T:before,T:B,T:after", string.Join(",", order), "order");
                }),
                Check.Async(s, "ADP-21" + c, "RunAllAsync reports a setup failure", async ct =>
                {
                    InvalidOperationException setup = new InvalidOperationException("setup failed");
                    AggregateException ex = await TestAssert.ThrowsAsync<AggregateException>(
                        () => probe.RunAllAsync(new[] { Fixtures.HookedSuite("R", _ => throw setup, null, Fixtures.Passing("R", "A")) }, null, ct), "setup");
                    TestAssert.True(ex.InnerExceptions.Contains(setup), "hook exception");
                    TestAssert.Contains("before_suite", ex.Message, "stage");
                }),
                Check.Async(s, "ADP-22" + c, "RunAllAsync forwards the token to hooks and cases", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        List<CancellationToken> seen = new List<CancellationToken>();
                        await probe.RunAllAsync(new[] { Fixtures.HookedSuite("R", t => { seen.Add(t); return default; }, null, Fixtures.Passing("R", "A", t => seen.Add(t))) }, null, source.Token);
                        TestAssert.True(seen.Count == 2 && seen.All(t => t == source.Token), "tokens");
                    }
                }),
                Check.Async(s, "ADP-23" + c, "RunAllAsync honors the filter", async ct =>
                {
                    TestRunOptions options = new TestRunOptions();
                    options.Filter.ExcludeTags.Add("broken");
                    await probe.RunAllAsync(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A"), new TestCaseDescriptor("R", "B", "B", _ => throw new InvalidOperationException("x"), new[] { "broken" })) }, options, ct);
                }),
                Check.Async(s, "ADP-26" + c, "Rows carry CaseTimeout: a hanging case fails with CaseTimeoutException after a round trip", async ct =>
                {
                    TestRunOptions options = new TestRunOptions { CaseTimeout = TimeSpan.FromMilliseconds(100) };
                    AdapterRow row = probe.RoundTrip(probe.Rows(AdapterFixtureSet.Cancel, options).Single());
                    CaseTimeoutException ex = await TestAssert.ThrowsAsync<CaseTimeoutException>(() => probe.ExecuteAsync(row, options, ct), "timed out");
                    TestAssert.Equal("AdpA.Wait", ex.TestId, "test id");
                    TestAssert.Equal(TimeSpan.FromMilliseconds(100), ex.Timeout, "timeout");
                }),
                Check.Sync(s, "ADP-24" + c, "The framework version meets the documented minimum", () =>
                {
                    TestAssert.True(probe.FrameworkVersion >= probe.MinimumVersion, probe.FrameworkVersion + " >= " + probe.MinimumVersion);
                }),
                Check.Sync(s, "DUP-08" + c, "The source rejects duplicate suite identities", () =>
                {
                    TestAssert.Throws<DuplicateTestIdException>(() => probe.Rows(AdapterFixtureSet.Duplicate, null), "duplicate");
                }),
                TelemetrySuites.Guarded(s, "TEL-12" + c, "RunAllAsync emits a run span and case metrics", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        await TestAssert.ThrowsAsync<AggregateException>(
                            () => probe.RunAllAsync(new[] { Fixtures.Suite("R", Fixtures.Passing("R", "A"), Fixtures.Failing("R", "B")) }, null, ct), "run");
                        TestAssert.Equal(ActivityStatusCode.Error, capture.Span(TouchstoneTelemetry.RunSpanName).Status, "run span");
                        TestAssert.Equal(2, capture.Measurements(TouchstoneTelemetry.Cases).Count, "case metrics");
                    }
                }),
                Check.Async(s, "CAN-22" + c, "RunAllAsync throws OperationCanceledException when canceled", async ct =>
                {
                    using (CancellationTokenSource source = new CancellationTokenSource())
                    {
                        await TestAssert.ThrowsAsync<OperationCanceledException>(
                            () => probe.RunAllAsync(new[] { Fixtures.Suite("R", Fixtures.CancelsRun("R", "A", source), Fixtures.Passing("R", "B")) }, null, source.Token), "canceled");
                    }
                }),
            });
        }
    }
}
