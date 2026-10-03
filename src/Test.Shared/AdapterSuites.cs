namespace Test.Shared
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;
    using Touchstone.NunitAdapter;
    using Touchstone.XunitAdapter;

    /// <summary>
    /// Suites covering the xUnit, NUnit, and MSTest adapter types. The adapters are exercised directly,
    /// without a host test framework, so the same assertions run under every runner.
    /// </summary>
    public static class AdapterSuites
    {
        /// <summary>
        /// Suite covering <see cref="TouchstoneTheoryData"/> and <see cref="TouchstoneTestBase"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor XunitDataSuite()
        {
            const string S = "XunitData";

            return new TestSuiteDescriptor(S, "xUnit theory data", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "SingleSuiteRows", "TouchstoneTheoryData yields one single-value row per case of a suite", () =>
                {
                    TestCaseDescriptor a = Fixtures.Passing("X", "A");
                    TestCaseDescriptor b = Fixtures.Passing("X", "B");
                    List<object[]> rows = Rows(new TouchstoneTheoryData(Fixtures.Suite("X", a, b)));

                    TestAssert.Equal(2, rows.Count, "row count");
                    TestAssert.Equal(1, rows[0].Length, "row width");
                    TestAssert.Same(a, rows[0][0], "first row");
                    TestAssert.Same(b, rows[1][0], "second row");
                }),

                CaseBuilder.Sync(S, "MultipleSuitesRows", "TouchstoneTheoryData flattens multiple suites in order", () =>
                {
                    TestCaseDescriptor a = Fixtures.Passing("X", "A");
                    TestCaseDescriptor b = Fixtures.Passing("Y", "B");
                    TestCaseDescriptor c = Fixtures.Passing("Y", "C");
                    List<object[]> rows = Rows(new TouchstoneTheoryData(
                        new List<TestSuiteDescriptor> { Fixtures.Suite("X", a), Fixtures.Suite("Y", b, c) }));

                    TestAssert.Equal(3, rows.Count, "row count");
                    TestAssert.Same(a, rows[0][0], "first row");
                    TestAssert.Same(b, rows[1][0], "second row");
                    TestAssert.Same(c, rows[2][0], "third row");
                }),

                CaseBuilder.Sync(S, "IncludesSkippedCases", "TouchstoneTheoryData includes skipped cases (unlike the NUnit and MSTest sources)", () =>
                {
                    List<object[]> rows = Rows(new TouchstoneTheoryData(
                        Fixtures.Suite("X", Fixtures.Passing("X", "A"), Fixtures.Skipped("X", "B"))));
                    TestAssert.Equal(2, rows.Count, "row count");
                }),

                CaseBuilder.Sync(S, "EmptyInputs", "TouchstoneTheoryData handles empty suites and empty suite lists", () =>
                {
                    TestAssert.Equal(0, Rows(new TouchstoneTheoryData(Fixtures.Suite("X"))).Count, "empty suite");
                    TestAssert.Equal(0, Rows(new TouchstoneTheoryData(new List<TestSuiteDescriptor>())).Count, "empty list");
                }),

                CaseBuilder.Sync(S, "NullSuiteThrows", "TouchstoneTheoryData rejects a null suite", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TouchstoneTheoryData((TestSuiteDescriptor)null!), "null suite");
                    TestAssert.Equal("suite", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "NullSuitesThrows", "TouchstoneTheoryData rejects a null suite list", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TouchstoneTheoryData((IEnumerable<TestSuiteDescriptor>)null!), "null suites");
                    TestAssert.Equal("suites", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "TestBaseExposesSuites", "TouchstoneTestBase.TestCases exposes the subclass suites as theory data", () =>
                {
                    TestCaseDescriptor a = Fixtures.Passing("X", "A");
                    TestCaseDescriptor b = Fixtures.Passing("Y", "B");
                    XunitTheoryHarness harness = new XunitTheoryHarness(
                        new List<TestSuiteDescriptor> { Fixtures.Suite("X", a), Fixtures.Suite("Y", b) });

                    List<object[]> rows = Rows(harness.TestCases);
                    TestAssert.Equal(2, rows.Count, "row count");
                    TestAssert.Same(a, rows[0][0], "first row");
                    TestAssert.Same(b, rows[1][0], "second row");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="TouchstoneTestCaseSource"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor NunitSourceSuite()
        {
            const string S = "NunitSource";

            return new TestSuiteDescriptor(S, "NUnit test case source", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "YieldsNonSkippedInOrder", "TouchstoneTestCaseSource yields non-skipped cases across suites in order", () =>
                {
                    TestCaseDescriptor a = Fixtures.Passing("X", "A");
                    TestCaseDescriptor c = Fixtures.Passing("Y", "C");
                    TouchstoneTestCaseSource source = new TouchstoneTestCaseSource(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X", a, Fixtures.Skipped("X", "B")),
                        Fixtures.Suite("Y", c),
                    });

                    List<object> items = Items(source);
                    TestAssert.Equal(2, items.Count, "item count");
                    TestAssert.Same(a, items[0], "first item");
                    TestAssert.Same(c, items[1], "second item");
                }),

                CaseBuilder.Sync(S, "Reenumerable", "TouchstoneTestCaseSource can be enumerated more than once", () =>
                {
                    TouchstoneTestCaseSource source = new TouchstoneTestCaseSource(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X", Fixtures.Passing("X", "A"), Fixtures.Passing("X", "B")),
                    });
                    TestAssert.Equal(2, Items(source).Count, "first enumeration");
                    TestAssert.Equal(2, Items(source).Count, "second enumeration");
                }),

                CaseBuilder.Sync(S, "AllSkippedIsEmpty", "TouchstoneTestCaseSource is empty when every case is skipped", () =>
                {
                    TouchstoneTestCaseSource source = new TouchstoneTestCaseSource(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X", Fixtures.Skipped("X", "A")),
                        Fixtures.Suite("Y"),
                    });
                    TestAssert.Equal(0, Items(source).Count, "item count");
                }),

                CaseBuilder.Sync(S, "NullSuitesThrows", "TouchstoneTestCaseSource rejects null suites at construction", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TouchstoneTestCaseSource(null!), "null suites");
                    TestAssert.Equal("suites", ex.ParamName, "ParamName");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="TouchstoneDynamicData"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor MstestDataSuite()
        {
            const string S = "MstestData";

            return new TestSuiteDescriptor(S, "MSTest dynamic data", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "YieldsNonSkippedRows", "TouchstoneDynamicData yields single-value rows for non-skipped cases in order", () =>
                {
                    TestCaseDescriptor a = Fixtures.Passing("X", "A");
                    TestCaseDescriptor c = Fixtures.Passing("Y", "C");
                    List<object[]> rows = new List<object[]>(TouchstoneDynamicData.FromSuites(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X", a, Fixtures.Skipped("X", "B")),
                        Fixtures.Suite("Y", c),
                    }));

                    TestAssert.Equal(2, rows.Count, "row count");
                    TestAssert.Equal(1, rows[0].Length, "row width");
                    TestAssert.Same(a, rows[0][0], "first row");
                    TestAssert.Same(c, rows[1][0], "second row");
                }),

                CaseBuilder.Sync(S, "EmptyInputs", "TouchstoneDynamicData yields nothing for empty input", () =>
                {
                    List<object[]> rows = new List<object[]>(TouchstoneDynamicData.FromSuites(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X"),
                        Fixtures.Suite("Y", Fixtures.Skipped("Y", "A")),
                    }));
                    TestAssert.Equal(0, rows.Count, "row count");
                }),

                CaseBuilder.Sync(S, "NullSuitesThrowsOnEnumeration", "TouchstoneDynamicData rejects null suites when enumerated", () =>
                {
                    IEnumerable<object[]> rows = TouchstoneDynamicData.FromSuites(null!);
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new List<object[]>(rows), "null suites");
                    TestAssert.Equal("suites", ex.ParamName, "ParamName");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="TouchstoneFactBase"/> RunAllAsync.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor XunitFactBaseSuite()
        {
            return RunAllSuite("XunitFactBase", "xUnit TouchstoneFactBase",
                (suites, ct) => new XunitFactHarness(suites).InvokeRunAllAsync(ct));
        }

        /// <summary>
        /// Suite covering <see cref="TouchstoneNunitBase"/> RunAllAsync.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor NunitBaseSuite()
        {
            return RunAllSuite("NunitBase", "NUnit TouchstoneNunitBase",
                (suites, ct) => new NunitBaseHarness(suites).InvokeRunAllAsync(ct));
        }

        /// <summary>
        /// Suite covering <see cref="TouchstoneMstestBase"/> RunAllAsync.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor MstestBaseSuite()
        {
            return RunAllSuite("MstestBase", "MSTest TouchstoneMstestBase",
                (suites, ct) => new MstestBaseHarness(suites).InvokeRunAllAsync(ct));
        }

        private static TestSuiteDescriptor RunAllSuite(
            string suiteId,
            string label,
            Func<IReadOnlyList<TestSuiteDescriptor>, CancellationToken, Task> runAll)
        {
            string S = suiteId;

            return new TestSuiteDescriptor(S, label, new List<TestCaseDescriptor>
            {
                CaseBuilder.Async(S, "AllPass", label + " RunAllAsync completes when every case passes", async ct =>
                {
                    int runs = 0;
                    await runAll(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X", Fixtures.Passing("X", "A", _ => runs++)),
                        Fixtures.Suite("Y", Fixtures.Passing("Y", "B", _ => runs++)),
                    }, ct).ConfigureAwait(false);
                    TestAssert.Equal(2, runs, "cases executed");
                }),

                CaseBuilder.Async(S, "EmptySuites", label + " RunAllAsync completes for an empty suite list", async ct =>
                {
                    await runAll(new List<TestSuiteDescriptor>(), ct).ConfigureAwait(false);
                }),

                CaseBuilder.Async(S, "FailuresAggregated", label + " RunAllAsync throws AggregateException listing every failure", async ct =>
                {
                    bool laterRan = false;
                    AggregateException ex = await TestAssert.ThrowsAsync<AggregateException>(
                        () => runAll(new List<TestSuiteDescriptor>
                        {
                            Fixtures.Suite("X", Fixtures.Failing("X", "A", "first broke"), Fixtures.Passing("X", "B")),
                            Fixtures.Suite("Y", Fixtures.Failing("Y", "C", "second broke"), Fixtures.Passing("Y", "D", _ => laterRan = true)),
                        }, ct),
                        "failing run").ConfigureAwait(false);

                    TestAssert.Contains("2 test(s) failed", ex.Message, "Message");
                    TestAssert.Contains("X.A: first broke", ex.Message, "Message");
                    TestAssert.Contains("Y.C: second broke", ex.Message, "Message");
                    TestAssert.DoesNotContain("X.B", ex.Message, "Message");
                    TestAssert.True(laterRan, "cases after a failure still executed");
                }),

                CaseBuilder.Async(S, "SkippedNotExecuted", label + " RunAllAsync does not execute skipped cases", async ct =>
                {
                    await runAll(new List<TestSuiteDescriptor>
                    {
                        Fixtures.Suite("X", Fixtures.Skipped("X", "A"), Fixtures.Passing("X", "B")),
                    }, ct).ConfigureAwait(false);
                }),

                CaseBuilder.Async(S, "HooksPerSuite", label + " RunAllAsync runs each suite's hooks around its own cases", async ct =>
                {
                    List<string> log = new List<string>();
                    List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>
                    {
                        HookedSuite("X", log, Fixtures.Passing("X", "A", _ => log.Add("X.A"))),
                        HookedSuite("Y", log, Fixtures.Passing("Y", "B", _ => log.Add("Y.B"))),
                    };

                    await runAll(suites, ct).ConfigureAwait(false);

                    TestAssert.Equal("before:X,X.A,after:X,before:Y,Y.B,after:Y", string.Join(",", log), "lifecycle order");
                }),

                CaseBuilder.Async(S, "AfterHookRunsOnFailure", label + " RunAllAsync runs AfterSuite when cases fail", async ct =>
                {
                    List<string> log = new List<string>();
                    await TestAssert.ThrowsAsync<AggregateException>(
                        () => runAll(new List<TestSuiteDescriptor> { HookedSuite("X", log, Fixtures.Failing("X", "A", "a")) }, ct),
                        "failing run").ConfigureAwait(false);
                    TestAssert.Equal("before:X,after:X", string.Join(",", log), "lifecycle order");
                }),

                CaseBuilder.Async(S, "BeforeHookFailurePropagates", label + " RunAllAsync propagates a BeforeSuite failure", async ct =>
                {
                    bool caseRan = false;
                    TestSuiteDescriptor suite = new TestSuiteDescriptor(
                        "X", "X",
                        new List<TestCaseDescriptor> { Fixtures.Passing("X", "A", _ => caseRan = true) },
                        beforeSuiteAsync: _ => throw new InvalidOperationException("setup failed"));

                    await TestAssert.ThrowsAsync<InvalidOperationException>(
                        () => runAll(new List<TestSuiteDescriptor> { suite }, ct), "setup failure").ConfigureAwait(false);
                    TestAssert.False(caseRan, "case executed");
                }),

                CaseBuilder.Async(S, "TokenForwarded", label + " RunAllAsync forwards the cancellation token to hooks and cases", async _ =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        List<CancellationToken> seen = new List<CancellationToken>();
                        TestSuiteDescriptor suite = new TestSuiteDescriptor(
                            "X", "X",
                            new List<TestCaseDescriptor> { Fixtures.Passing("X", "A", t => seen.Add(t)) },
                            beforeSuiteAsync: t => { seen.Add(t); return ValueTask.CompletedTask; },
                            afterSuiteAsync: t => { seen.Add(t); return ValueTask.CompletedTask; });

                        await runAll(new List<TestSuiteDescriptor> { suite }, cts.Token).ConfigureAwait(false);

                        TestAssert.Equal(3, seen.Count, "token count");
                        foreach (CancellationToken t in seen)
                            TestAssert.True(t == cts.Token, "received the caller token");
                    }
                }),
            });
        }

        private static TestSuiteDescriptor HookedSuite(string suiteId, List<string> log, params TestCaseDescriptor[] cases)
        {
            return new TestSuiteDescriptor(
                suiteId, suiteId,
                new List<TestCaseDescriptor>(cases),
                beforeSuiteAsync: _ => { log.Add("before:" + suiteId); return ValueTask.CompletedTask; },
                afterSuiteAsync: _ => { log.Add("after:" + suiteId); return ValueTask.CompletedTask; });
        }

        private static List<object[]> Rows(IEnumerable<object[]> data)
        {
            return new List<object[]>(data);
        }

        private static List<object> Items(IEnumerable source)
        {
            List<object> items = new List<object>();
            foreach (object item in source)
                items.Add(item);
            return items;
        }
    }
}
