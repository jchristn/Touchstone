namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Catalog sections 11.1 to 11.4: case descriptors, suite descriptors, duplicate identity, and immutability.
    /// </summary>
    public static class DescriptorSuites
    {
        private static readonly Func<CancellationToken, Task> _Noop = _ => Task.CompletedTask;

        /// <summary>
        /// Every suite in this file.
        /// </summary>
        /// <returns>The suites.</returns>
        public static IEnumerable<TestSuiteDescriptor> All()
        {
            yield return CaseDescriptorSuite();
            yield return SuiteDescriptorSuite();
            yield return DuplicateSuite();
            yield return ImmutabilitySuite();
        }

        private static TestSuiteDescriptor CaseDescriptorSuite()
        {
            const string S = "Descriptor";
            return new TestSuiteDescriptor(S, "Case descriptor", new[]
            {
                Check.Sync(S, "DESC-01", "Constructor stores every value", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("Suite", "Case", "Name", _Noop, new[] { "fast" }, "later");
                    TestAssert.Equal("Suite", d.SuiteId, "suite");
                    TestAssert.Equal("Case", d.CaseId, "case");
                    TestAssert.Equal("Name", d.DisplayName, "name");
                    TestAssert.Equal("fast", d.Tags.Single(), "tag");
                    TestAssert.Equal("later", d.SkipReason, "skip reason");
                    TestAssert.True(d.IsSkipped, "skipped");
                }),
                Check.Sync(S, "DESC-02", "Optional parameters default to no tags and no skip", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("Suite", "Case", "Name", _Noop);
                    TestAssert.Equal(0, d.Tags.Count, "tags");
                    TestAssert.Null(d.SkipReason, "skip reason");
                    TestAssert.False(d.IsSkipped, "skipped");
                }),
                Check.Sync(S, "DESC-03", "Null required arguments throw ArgumentNullException naming the parameter", () =>
                {
                    TestAssert.Equal("suiteId", TestAssert.Throws<ArgumentNullException>(() => new TestCaseDescriptor(null!, "C", "N", _Noop), "suite").ParamName, "suite param");
                    TestAssert.Equal("caseId", TestAssert.Throws<ArgumentNullException>(() => new TestCaseDescriptor("S", null!, "N", _Noop), "case").ParamName, "case param");
                    TestAssert.Equal("displayName", TestAssert.Throws<ArgumentNullException>(() => new TestCaseDescriptor("S", "C", null!, _Noop), "name").ParamName, "name param");
                    TestAssert.Equal("executeAsync", TestAssert.Throws<ArgumentNullException>(() => new TestCaseDescriptor("S", "C", "N", null!), "body").ParamName, "body param");
                }),
                Check.Sync(S, "DESC-04", "Empty or blank identities and names throw ArgumentException", () =>
                {
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("", "C", "N", _Noop), "empty suite");
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", "", "N", _Noop), "empty case");
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", "C", "  ", _Noop), "blank name");
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor(" ", "C", "N", _Noop), "blank suite");
                }),
                Check.Sync(S, "DESC-05", "A suite identity containing '.' is rejected", () =>
                {
                    ArgumentException ex = TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("A.B", "C", "N", _Noop), "dotted suite");
                    TestAssert.Contains("'.'", ex.Message, "message explains the separator");
                }),
                Check.Sync(S, "DESC-06", "Identity characters outside the grammar are rejected", () =>
                {
                    foreach (string bad in new[] { "a b", "a/b", "a[b]", "café", "a:b" })
                    {
                        TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor(bad, "C", "N", _Noop), "suite " + bad);
                        TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", bad, "N", _Noop), "case " + bad);
                    }
                }),
                Check.Sync(S, "DESC-07", "Case identity allows '.', '_' and '-'", () =>
                {
                    TestAssert.Equal("S.a.b_c-d", new TestCaseDescriptor("S", "a.b_c-d", "N", _Noop).TestId, "test id");
                }),
                Check.Sync(S, "DESC-08", "TestId is SuiteId.CaseId", () =>
                {
                    TestAssert.Equal("Cart.Add", new TestCaseDescriptor("Cart", "Add", "N", _Noop).TestId, "test id");
                }),
                Check.Sync(S, "DESC-09", "Identities cannot collide by moving the separator", () =>
                {
                    TestCaseDescriptor valid = new TestCaseDescriptor("A", "B.C", "N", _Noop);
                    TestAssert.Equal("A.B.C", valid.TestId, "valid id");
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("A.B", "C", "N", _Noop), "colliding suite");
                }),
                Check.Sync(S, "DESC-10", "A skip reason marks the case skipped", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "N", _Noop, null, "blocked");
                    TestAssert.True(d.IsSkipped, "skipped");
                    TestAssert.Equal("blocked", d.SkipReason, "reason");
                }),
                Check.Sync(S, "DESC-11", "A blank skip reason is rejected", () =>
                {
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", "C", "N", _Noop, null, " "), "blank reason");
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", "C", "N", _Noop, null, ""), "empty reason");
                }),
                Check.Sync(S, "DESC-12", "Null tags normalize to an empty list", () =>
                {
                    TestAssert.NotNull(new TestCaseDescriptor("S", "C", "N", _Noop, null).Tags, "tags");
                }),
                Check.Sync(S, "DESC-13", "Tags are copied from the caller", () =>
                {
                    List<string> tags = new List<string> { "a" };
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "N", _Noop, tags);
                    tags.Add("b");
                    TestAssert.Equal(1, d.Tags.Count, "tag count after caller change");
                }),
                Check.Sync(S, "DESC-14", "Invalid tags are rejected", () =>
                {
                    foreach (string bad in new[] { "", "a b", "a,b", "a/b" })
                        TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", "C", "N", _Noop, new[] { bad }), "tag '" + bad + "'");
                    TestAssert.Throws<ArgumentException>(() => new TestCaseDescriptor("S", "C", "N", _Noop, new string[] { null! }), "null tag");
                    TestAssert.Equal(3, new TestCaseDescriptor("S", "C", "N", _Noop, new[] { "a.b", "c:d", "e_f-g" }).Tags.Count, "valid tags");
                }),
                Check.Sync(S, "DESC-15", "Tags that differ only by case collapse to the first spelling", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "N", _Noop, new[] { "Fast", "fast", "FAST", "slow" });
                    TestAssert.Equal("Fast,slow", string.Join(",", d.Tags), "tags");
                }),
                Check.Sync(S, "DESC-16", "ToString returns the display name", () =>
                {
                    TestAssert.Equal("Friendly", new TestCaseDescriptor("S", "C", "Friendly", _Noop).ToString(), "to string");
                }),
                Check.Sync(S, "DESC-17", "Metadata is null for ordinary cases and set for scenarios", () =>
                {
                    TestAssert.Null(new TestCaseDescriptor("S", "C", "N", _Noop).Metadata, "ordinary");
                    TestAssert.NotNull(Fixtures.Scenario("S", "C", (c, ct) => Task.CompletedTask).Metadata, "scenario");
                }),
                Check.Sync(S, "DESC-18", "The tag list cannot be mutated through a cast", () =>
                {
                    TestCaseDescriptor d = new TestCaseDescriptor("S", "C", "N", _Noop, new[] { "a" });
                    if (d.Tags is IList<string> list)
                        TestAssert.Throws<NotSupportedException>(() => list.Add("b"), "add through cast");
                    TestAssert.Equal(1, d.Tags.Count, "tags unchanged");
                }),
            });
        }

        private static TestSuiteDescriptor SuiteDescriptorSuite()
        {
            const string S = "Suite";
            return new TestSuiteDescriptor(S, "Suite descriptor", new[]
            {
                Check.Sync(S, "SUITE-01", "Constructor stores every value", () =>
                {
                    Func<CancellationToken, ValueTask> before = _ => default;
                    Func<CancellationToken, ValueTask> after = _ => default;
                    TestCaseDescriptor c = Fixtures.Passing("X", "One");
                    TestSuiteDescriptor s = new TestSuiteDescriptor("X", "Display", new[] { c }, before, after);
                    TestAssert.Equal("X", s.SuiteId, "id");
                    TestAssert.Equal("Display", s.DisplayName, "name");
                    TestAssert.Same(c, s.Cases[0], "case");
                    TestAssert.Same(before, s.BeforeSuiteAsync, "before");
                    TestAssert.Same(after, s.AfterSuiteAsync, "after");
                }),
                Check.Sync(S, "SUITE-02", "Hooks default to null", () =>
                {
                    TestSuiteDescriptor s = Fixtures.Suite("X");
                    TestAssert.Null(s.BeforeSuiteAsync, "before");
                    TestAssert.Null(s.AfterSuiteAsync, "after");
                }),
                Check.Sync(S, "SUITE-03", "Null required arguments throw ArgumentNullException", () =>
                {
                    TestAssert.Throws<ArgumentNullException>(() => new TestSuiteDescriptor(null!, "D", Array.Empty<TestCaseDescriptor>()), "id");
                    TestAssert.Throws<ArgumentNullException>(() => new TestSuiteDescriptor("X", null!, Array.Empty<TestCaseDescriptor>()), "name");
                    TestAssert.Throws<ArgumentNullException>(() => new TestSuiteDescriptor("X", "D", null!), "cases");
                }),
                Check.Sync(S, "SUITE-04", "Blank or invalid suite identities and names are rejected", () =>
                {
                    TestAssert.Throws<ArgumentException>(() => new TestSuiteDescriptor("", "D", Array.Empty<TestCaseDescriptor>()), "empty id");
                    TestAssert.Throws<ArgumentException>(() => new TestSuiteDescriptor("A.B", "D", Array.Empty<TestCaseDescriptor>()), "dotted id");
                    TestAssert.Throws<ArgumentException>(() => new TestSuiteDescriptor("X", " ", Array.Empty<TestCaseDescriptor>()), "blank name");
                }),
                Check.Sync(S, "SUITE-05", "A null case in the list is rejected", () =>
                {
                    TestAssert.Throws<ArgumentException>(() => new TestSuiteDescriptor("X", "D", new TestCaseDescriptor[] { null! }), "null case");
                }),
                Check.Sync(S, "SUITE-06", "A case from another suite is rejected, naming both identities", () =>
                {
                    ArgumentException ex = TestAssert.Throws<ArgumentException>(() => new TestSuiteDescriptor("X", "D", new[] { Fixtures.Passing("Y", "One") }), "foreign case");
                    TestAssert.Contains("Y", ex.Message, "case suite named");
                    TestAssert.Contains("X", ex.Message, "target suite named");
                }),
                Check.Sync(S, "SUITE-07", "Cases are copied from the caller", () =>
                {
                    List<TestCaseDescriptor> cases = new List<TestCaseDescriptor> { Fixtures.Passing("X", "One") };
                    TestSuiteDescriptor s = new TestSuiteDescriptor("X", "D", cases);
                    cases.Add(Fixtures.Passing("X", "Two"));
                    TestAssert.Equal(1, s.Cases.Count, "case count");
                }),
                Check.Sync(S, "SUITE-08", "An empty case list is allowed", () =>
                {
                    TestAssert.Equal(0, Fixtures.Suite("X").Cases.Count, "count");
                }),
                Check.Sync(S, "SUITE-09", "ToString returns the display name", () =>
                {
                    TestAssert.Equal("Display", new TestSuiteDescriptor("X", "Display", Array.Empty<TestCaseDescriptor>()).ToString(), "to string");
                }),
            });
        }

        private static TestSuiteDescriptor DuplicateSuite()
        {
            const string S = "Duplicate";
            return new TestSuiteDescriptor(S, "Duplicate identity", new[]
            {
                Check.Sync(S, "DUP-01", "Duplicate case identities in one suite are rejected", () =>
                {
                    DuplicateTestIdException ex = TestAssert.Throws<DuplicateTestIdException>(
                        () => Fixtures.Suite("X", Fixtures.Passing("X", "One"), Fixtures.Passing("X", "One")), "duplicate");
                    TestAssert.Equal("X.One", ex.DuplicateId, "duplicate id");
                    TestAssert.Contains("X.One", ex.Message, "message");
                }),
                Check.Sync(S, "DUP-02", "Case identity comparison is ordinal", () =>
                {
                    TestAssert.Equal(2, Fixtures.Suite("X", Fixtures.Passing("X", "Case"), Fixtures.Passing("X", "case")).Cases.Count, "both kept");
                }),
                Check.Async(S, "DUP-03", "Duplicate suite identities fail before anything runs", async ct =>
                {
                    bool ran = false;
                    RecordingSink sink = new RecordingSink();
                    using (TelemetryCapture capture = new TelemetryCapture())
                    {
                        TestSuiteDescriptor a = Fixtures.Suite("X", Fixtures.Passing("X", "One", _ => ran = true));
                        TestSuiteDescriptor b = Fixtures.Suite("X", Fixtures.Passing("X", "Two", _ => ran = true));
                        DuplicateTestIdException ex = await TestAssert.ThrowsAsync<DuplicateTestIdException>(
                            () => TestExecutor.RunAsync(new[] { a, b }, sink, null, ct), "duplicate suites");
                        TestAssert.Equal("X", ex.DuplicateId, "duplicate id");
                        TestAssert.Equal(0, capture.Spans().Count, "no spans");
                    }

                    TestAssert.False(ran, "no case ran");
                    TestAssert.Equal(0, sink.Events.Count, "no sink events");
                }),
                Check.Async(S, "DUP-04", "The same suite instance twice is rejected", async ct =>
                {
                    TestSuiteDescriptor a = Fixtures.Suite("X", Fixtures.Passing("X", "One"));
                    await TestAssert.ThrowsAsync<DuplicateTestIdException>(() => TestExecutor.RunAsync(new[] { a, a }, null, null, ct), "same instance");
                }),
                Check.Sync(S, "DUP-05", "DuplicateTestIdException is an ArgumentException with the duplicate identity", () =>
                {
                    DuplicateTestIdException ex = new DuplicateTestIdException("A.B", "message", "param");
                    TestAssert.True(ex is ArgumentException, "derives from ArgumentException");
                    TestAssert.Equal("A.B", ex.DuplicateId, "id");
                    TestAssert.Equal("param", ex.ParamName, "param");
                    TestAssert.Equal(string.Empty, new DuplicateTestIdException().DuplicateId, "default id");
                }),
                Check.Sync(S, "DUP-06", "Scenario examples reject duplicate example and test identities", () =>
                {
                    ScenarioExample<int>[] same = { new ScenarioExample<int>("a", 1), new ScenarioExample<int>("a", 2) };
                    TestAssert.Throws<ArgumentException>(() => ScenarioExamples.Expand(same, e => Fixtures.Passing("X", e.ExampleId)), "duplicate example");
                    ScenarioExample<int>[] distinct = { new ScenarioExample<int>("a", 1), new ScenarioExample<int>("b", 2) };
                    TestAssert.Throws<ArgumentException>(() => ScenarioExamples.Expand(distinct, e => Fixtures.Passing("X", "Same")), "duplicate test");
                }),
                Check.Sync(S, "DUP-07", "Scenario builders reject duplicate step identities", () =>
                {
                    ScenarioBuilder<ScenarioContext> builder = new ScenarioBuilder<ScenarioContext>("X", "C", "N", _ => Task.FromResult(new ScenarioContext()))
                        .Given("one", "first", (c, ct) => Task.CompletedTask);
                    TestAssert.Throws<ArgumentException>(() => builder.When("one", "again", (c, ct) => Task.CompletedTask), "duplicate step");
                }),
            });
        }

        private static TestSuiteDescriptor ImmutabilitySuite()
        {
            const string S = "Immutability";
            Type[] models =
            {
                typeof(TestCaseDescriptor), typeof(TestSuiteDescriptor), typeof(TestResult), typeof(TestRunSummary),
                typeof(ScenarioStepResult), typeof(ScenarioMetadata), typeof(SuiteLifecycleError),
            };

            return new TestSuiteDescriptor(S, "Immutability and copy methods", new[]
            {
                Check.Sync(S, "IMM-01", "WithTags returns a copy with new tags", () =>
                {
                    TestCaseDescriptor original = Fixtures.Passing("X", "One", null, new[] { "a" });
                    TestCaseDescriptor copy = original.WithTags(new[] { "b", "c" });
                    TestAssert.Equal("a", string.Join(",", original.Tags), "original");
                    TestAssert.Equal("b,c", string.Join(",", copy.Tags), "copy");
                    TestAssert.Equal(original.TestId, copy.TestId, "identity kept");
                }),
                Check.Sync(S, "IMM-02", "WithSkip returns a skipped copy", () =>
                {
                    TestCaseDescriptor original = Fixtures.Passing("X", "One");
                    TestCaseDescriptor copy = original.WithSkip("later");
                    TestAssert.False(original.IsSkipped, "original");
                    TestAssert.Equal("later", copy.SkipReason, "copy");
                }),
                Check.Sync(S, "IMM-03", "WithoutSkip returns an unskipped copy", () =>
                {
                    TestCaseDescriptor original = Fixtures.Skipped("X", "One");
                    TestAssert.False(original.WithoutSkip().IsSkipped, "copy");
                    TestAssert.True(original.IsSkipped, "original");
                }),
                Check.Sync(S, "IMM-04", "WithDisplayName returns a renamed copy", () =>
                {
                    TestCaseDescriptor original = Fixtures.Passing("X", "One");
                    TestAssert.Equal("Renamed", original.WithDisplayName("Renamed").DisplayName, "copy");
                    TestAssert.Equal("One display", original.DisplayName, "original");
                }),
                Check.Async(S, "IMM-05", "Copies of a scenario keep the scenario", async ct =>
                {
                    List<ScenarioContext> contexts = new List<ScenarioContext>();
                    TestCaseDescriptor scenario = Fixtures.Scenario("X", "One", (c, t) => Task.CompletedTask, contexts)
                        .WithTags(new[] { "a" }).WithDisplayName("Renamed").WithSkip("s").WithoutSkip();
                    TestResult result = await TestExecutor.ExecuteCaseResultAsync(scenario, null, null, ct);
                    TestAssert.Equal(TestOutcome.Passed, result.Outcome, "outcome");
                    TestAssert.Equal(1, result.Steps.Count, "steps kept");
                    TestAssert.NotNull(scenario.Metadata, "metadata kept");
                    TestAssert.Equal(1, contexts.Count, "context created");
                }),
                Check.Sync(S, "IMM-06", "Copy methods validate like the constructor", () =>
                {
                    TestCaseDescriptor d = Fixtures.Passing("X", "One");
                    TestAssert.Throws<ArgumentException>(() => d.WithTags(new[] { "a b" }), "tags");
                    TestAssert.Throws<ArgumentNullException>(() => d.WithSkip(null!), "null reason");
                    TestAssert.Throws<ArgumentException>(() => d.WithSkip(" "), "blank reason");
                    TestAssert.Throws<ArgumentException>(() => d.WithDisplayName(""), "blank name");
                }),
                Check.Async(S, "IMM-07", "A copy and its original run concurrently with separate contexts", async ct =>
                {
                    List<ScenarioContext> contexts = new List<ScenarioContext>();
                    TestCaseDescriptor original = Fixtures.Scenario("X", "One", async (c, t) =>
                    {
                        c.Value++;
                        await Task.Delay(5, t);
                        TestAssert.Equal(1, c.Value, "context not shared");
                    }, contexts);
                    TestCaseDescriptor copy = original.WithDisplayName("Copy");
                    TestResult[] results = await Task.WhenAll(
                        TestExecutor.ExecuteCaseResultAsync(original, null, null, ct),
                        TestExecutor.ExecuteCaseResultAsync(copy, null, null, ct));
                    TestAssert.True(results.All(r => r.Outcome == TestOutcome.Passed), "both passed");
                    TestAssert.Equal(2, contexts.Count, "two contexts");
                }),
                Check.Sync(S, "IMM-08", "Model types have no public or protected setters", () =>
                {
                    foreach (Type type in models)
                    {
                        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        {
                            MethodInfo? setter = property.GetSetMethod(true);
                            bool exposed = setter != null && (setter.IsPublic || setter.IsFamily || setter.IsFamilyOrAssembly);
                            TestAssert.False(exposed, type.Name + "." + property.Name + " has a public or protected setter");
                        }
                    }
                }),
                Check.Sync(S, "IMM-09", "Model collection properties are read-only interfaces", () =>
                {
                    foreach (Type type in models)
                    {
                        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        {
                            Type t = property.PropertyType;
                            bool mutable = t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>));
                            TestAssert.False(mutable, type.Name + "." + property.Name + " exposes a mutable collection type");
                        }
                    }
                }),
            });
        }
    }
}
