namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Suites covering <see cref="TestCaseDescriptor"/> and <see cref="TestSuiteDescriptor"/>.
    /// </summary>
    public static class DescriptorSuites
    {
        private static readonly Func<CancellationToken, Task> _Noop = _ => Task.CompletedTask;

        /// <summary>
        /// Suite covering <see cref="TestCaseDescriptor"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TestCaseDescriptorSuite()
        {
            const string S = "TestCaseDescriptor";

            return new TestSuiteDescriptor(S, "TestCaseDescriptor", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "CtorStoresValues", "TestCaseDescriptor constructor stores all values", () =>
                {
                    List<string> tags = new List<string> { "fast", "unit" };
                    TestCaseDescriptor d = new TestCaseDescriptor("Suite", "Case", "Display", _Noop, tags, true, "why");
                    TestAssert.Equal("Suite", d.SuiteId, "SuiteId");
                    TestAssert.Equal("Case", d.CaseId, "CaseId");
                    TestAssert.Equal("Display", d.DisplayName, "DisplayName");
                    TestAssert.Same(_Noop, d.ExecuteAsync, "ExecuteAsync");
                    TestAssert.Same(tags, d.Tags, "Tags");
                    TestAssert.True(d.Skip, "Skip");
                    TestAssert.Equal("why", d.SkipReason, "SkipReason");
                }),

                CaseBuilder.Sync(S, "CtorDefaults", "TestCaseDescriptor optional parameters default to no tags, not skipped, null reason", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "D", _Noop);
                    TestAssert.Equal(0, d.Tags.Count, "Tags.Count");
                    TestAssert.False(d.Skip, "Skip");
                    TestAssert.Null(d.SkipReason, "SkipReason");
                }),

                CaseBuilder.Sync(S, "CtorNullSuiteIdThrows", "TestCaseDescriptor constructor rejects null suiteId", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestCaseDescriptor(null!, "C", "D", _Noop), "null suiteId");
                    TestAssert.Equal("suiteId", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "CtorNullCaseIdThrows", "TestCaseDescriptor constructor rejects null caseId", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestCaseDescriptor("S", null!, "D", _Noop), "null caseId");
                    TestAssert.Equal("caseId", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "CtorNullDisplayNameThrows", "TestCaseDescriptor constructor rejects null displayName", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestCaseDescriptor("S", "C", null!, _Noop), "null displayName");
                    TestAssert.Equal("displayName", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "CtorNullExecuteAsyncThrows", "TestCaseDescriptor constructor rejects null executeAsync", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestCaseDescriptor("S", "C", "D", null!), "null executeAsync");
                    TestAssert.Equal("executeAsync", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "SettersRejectNull", "TestCaseDescriptor property setters reject null for required values", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "D", _Noop);
                    TestAssert.Throws<ArgumentNullException>(() => d.SuiteId = null!, "SuiteId setter");
                    TestAssert.Throws<ArgumentNullException>(() => d.CaseId = null!, "CaseId setter");
                    TestAssert.Throws<ArgumentNullException>(() => d.DisplayName = null!, "DisplayName setter");
                    TestAssert.Throws<ArgumentNullException>(() => d.ExecuteAsync = null!, "ExecuteAsync setter");
                    TestAssert.Equal("S.C", d.TestId, "TestId unchanged after rejected sets");
                }),

                CaseBuilder.Sync(S, "NullTagsBecomeEmpty", "TestCaseDescriptor null tags normalize to an empty list", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "D", _Noop, tags: null);
                    TestAssert.Equal(0, d.Tags.Count, "Tags.Count from ctor");
                    d.Tags = new List<string> { "x" };
                    d.Tags = null!;
                    TestAssert.NotNull(d.Tags, "Tags after null set");
                    TestAssert.Equal(0, d.Tags.Count, "Tags.Count after null set");
                }),

                CaseBuilder.Sync(S, "TestIdIsComposite", "TestCaseDescriptor TestId is SuiteId.CaseId", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("MySuite", "MyCase", "D", _Noop);
                    TestAssert.Equal("MySuite.MyCase", d.TestId, "TestId");
                }),

                CaseBuilder.Sync(S, "TestIdTracksSetters", "TestCaseDescriptor TestId reflects updated SuiteId and CaseId", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("A", "B", "D", _Noop);
                    d.SuiteId = "X";
                    d.CaseId = "Y";
                    TestAssert.Equal("X.Y", d.TestId, "TestId");
                }),

                CaseBuilder.Sync(S, "EmptyStringsAllowed", "TestCaseDescriptor accepts empty identifiers", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor(string.Empty, string.Empty, string.Empty, _Noop);
                    TestAssert.Equal(".", d.TestId, "TestId");
                    TestAssert.Equal(string.Empty, d.ToString(), "ToString");
                }),

                CaseBuilder.Sync(S, "ToStringReturnsDisplayName", "TestCaseDescriptor ToString returns DisplayName", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "My Display Name", _Noop);
                    TestAssert.Equal("My Display Name", d.ToString(), "ToString");
                    d.DisplayName = "Renamed";
                    TestAssert.Equal("Renamed", d.ToString(), "ToString after rename");
                }),

                CaseBuilder.Sync(S, "SkipIsMutable", "TestCaseDescriptor Skip and SkipReason can be changed after construction", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "D", _Noop);
                    d.Skip = true;
                    d.SkipReason = "later";
                    TestAssert.True(d.Skip, "Skip");
                    TestAssert.Equal("later", d.SkipReason, "SkipReason");
                    d.SkipReason = null;
                    TestAssert.Null(d.SkipReason, "SkipReason after clearing");
                }),
            });
        }

        /// <summary>
        /// Suite covering <see cref="TestSuiteDescriptor"/>.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TestSuiteDescriptorSuite()
        {
            const string S = "TestSuiteDescriptor";

            return new TestSuiteDescriptor(S, "TestSuiteDescriptor", new List<TestCaseDescriptor>
            {
                CaseBuilder.Sync(S, "CtorStoresValues", "TestSuiteDescriptor constructor stores all values", () =>
                {
                    List<TestCaseDescriptor> cases = new List<TestCaseDescriptor> { Fixtures.Passing("S", "C") };
                    Func<CancellationToken, ValueTask> before = _ => ValueTask.CompletedTask;
                    Func<CancellationToken, ValueTask> after = _ => ValueTask.CompletedTask;
                    TestSuiteDescriptor s = new TestSuiteDescriptor("S", "Suite", cases, before, after);
                    TestAssert.Equal("S", s.SuiteId, "SuiteId");
                    TestAssert.Equal("Suite", s.DisplayName, "DisplayName");
                    TestAssert.Same(cases, s.Cases, "Cases");
                    TestAssert.Same(before, s.BeforeSuiteAsync, "BeforeSuiteAsync");
                    TestAssert.Same(after, s.AfterSuiteAsync, "AfterSuiteAsync");
                }),

                CaseBuilder.Sync(S, "HooksDefaultNull", "TestSuiteDescriptor lifecycle hooks default to null", () =>
                {
                    TestSuiteDescriptor s = new TestSuiteDescriptor("S", "D", new List<TestCaseDescriptor>());
                    TestAssert.Null(s.BeforeSuiteAsync, "BeforeSuiteAsync");
                    TestAssert.Null(s.AfterSuiteAsync, "AfterSuiteAsync");
                    TestAssert.Equal(0, s.Cases.Count, "Cases.Count");
                }),

                CaseBuilder.Sync(S, "CtorNullSuiteIdThrows", "TestSuiteDescriptor constructor rejects null suiteId", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestSuiteDescriptor(null!, "D", new List<TestCaseDescriptor>()), "null suiteId");
                    TestAssert.Equal("suiteId", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "CtorNullDisplayNameThrows", "TestSuiteDescriptor constructor rejects null displayName", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestSuiteDescriptor("S", null!, new List<TestCaseDescriptor>()), "null displayName");
                    TestAssert.Equal("displayName", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "CtorNullCasesThrows", "TestSuiteDescriptor constructor rejects null cases", () =>
                {
                    ArgumentNullException ex = TestAssert.Throws<ArgumentNullException>(
                        () => new TestSuiteDescriptor("S", "D", null!), "null cases");
                    TestAssert.Equal("cases", ex.ParamName, "ParamName");
                }),

                CaseBuilder.Sync(S, "SettersRejectNull", "TestSuiteDescriptor property setters reject null for required values", () =>
                {
                    TestSuiteDescriptor s = new TestSuiteDescriptor("S", "D", new List<TestCaseDescriptor>());
                    TestAssert.Throws<ArgumentNullException>(() => s.SuiteId = null!, "SuiteId setter");
                    TestAssert.Throws<ArgumentNullException>(() => s.DisplayName = null!, "DisplayName setter");
                    TestAssert.Throws<ArgumentNullException>(() => s.Cases = null!, "Cases setter");
                }),

                CaseBuilder.Sync(S, "HooksCanBeCleared", "TestSuiteDescriptor hooks can be set and cleared", () =>
                {
                    TestSuiteDescriptor s = new TestSuiteDescriptor(
                        "S", "D", new List<TestCaseDescriptor>(),
                        _ => ValueTask.CompletedTask,
                        _ => ValueTask.CompletedTask);
                    s.BeforeSuiteAsync = null;
                    s.AfterSuiteAsync = null;
                    TestAssert.Null(s.BeforeSuiteAsync, "BeforeSuiteAsync");
                    TestAssert.Null(s.AfterSuiteAsync, "AfterSuiteAsync");
                }),

                CaseBuilder.Sync(S, "CasesReplaceable", "TestSuiteDescriptor Cases can be replaced", () =>
                {
                    TestSuiteDescriptor s = new TestSuiteDescriptor("S", "D", new List<TestCaseDescriptor>());
                    s.Cases = new List<TestCaseDescriptor> { Fixtures.Passing("S", "A"), Fixtures.Passing("S", "B") };
                    TestAssert.Equal(2, s.Cases.Count, "Cases.Count");
                }),

                CaseBuilder.Sync(S, "ToStringReturnsDisplayName", "TestSuiteDescriptor ToString returns DisplayName", () =>
                {
                    TestSuiteDescriptor s = new TestSuiteDescriptor("S", "Friendly Suite", new List<TestCaseDescriptor>());
                    TestAssert.Equal("Friendly Suite", s.ToString(), "ToString");
                }),
            });
        }
    }
}
