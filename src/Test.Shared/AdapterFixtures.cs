namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Suites behind each <see cref="AdapterFixtureSet"/>.
    /// </summary>
    public static class AdapterFixtures
    {
        /// <summary>
        /// Skip reason of AdpA.Skip.
        /// </summary>
        public const string SkipReason = "adapter skip reason";

        /// <summary>
        /// Failure message of AdpA.Fail.
        /// </summary>
        public const string FailureMessage = "adapter failure";

        /// <summary>
        /// Step name of AdpA.Scn, which appears in diagnostics.
        /// </summary>
        public const string StepName = "the adapter step";

        /// <summary>
        /// Suites for a fixture set.
        /// </summary>
        /// <param name="set">The set.</param>
        /// <returns>Fresh suites.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> Suites(AdapterFixtureSet set)
        {
            switch (set)
            {
                case AdapterFixtureSet.Standard:
                    return new[]
                    {
                        Fixtures.Suite("AdpA",
                            Fixtures.Passing("AdpA", "Pass", null, new[] { "fast", "db" }),
                            Fixtures.Failing("AdpA", "Fail", FailureMessage),
                            Fixtures.Skipped("AdpA", "Skip", null, SkipReason),
                            new ScenarioBuilder<ScenarioContext>("AdpA", "Scn", "Scn display", _ => Task.FromResult(new ScenarioContext()))
                                .Then("step", StepName, (_, _) => Task.CompletedTask)
                                .Build()),
                        Fixtures.Suite("AdpB", Fixtures.Passing("AdpB", "Other", null, new[] { "slow" })),
                    };
                case AdapterFixtureSet.Empty:
                    return Array.Empty<TestSuiteDescriptor>();
                case AdapterFixtureSet.EmptySuites:
                    return new[] { Fixtures.Suite("AdpA") };
                case AdapterFixtureSet.Duplicate:
                    return new[] { Fixtures.Suite("AdpA", Fixtures.Passing("AdpA", "One")), Fixtures.Suite("AdpA", Fixtures.Passing("AdpA", "Two")) };
                default:
                    return new[]
                    {
                        Fixtures.Suite("AdpA", new TestCaseDescriptor("AdpA", "Wait", "Wait display", async ct => await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false))),
                    };
            }
        }
    }
}
