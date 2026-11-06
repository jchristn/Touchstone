namespace Parity.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// A deliberately mixed suite whose native outcomes scripts/verify-parity.py compares across adapters.
    /// Some cases fail on purpose. The suite is not part of Touchstone.slnx.
    /// </summary>
    public sealed class ParitySuites : ITestSuiteProvider
    {
        /// <summary>
        /// Tag carried by the Pass case, used to check native category filters.
        /// </summary>
        public const string NativeTag = "parity-tag";

        /// <summary>
        /// Tag excluded by <see cref="Options"/>.
        /// </summary>
        public const string ExcludedTag = "parity-excluded";

        /// <summary>
        /// Test identity of the case that the host runs with a canceled token.
        /// </summary>
        public const string CancelTestId = "Parity.Cancel";

        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get
            {
                return new[]
                {
                    new TestSuiteDescriptor("Parity", "Parity", new[]
                    {
                        new TestCaseDescriptor("Parity", "Pass", "Parity pass", _ => Task.CompletedTask, new[] { NativeTag }),
                        Scenario("ScnPass", "Parity scenario pass", false),
                        new TestCaseDescriptor("Parity", "Fail", "Parity fail", _ => throw new InvalidOperationException("parity failure")),
                        Scenario("ScnFail", "Parity scenario fail", true),
                        new TestCaseDescriptor("Parity", "Skip", "Parity skip", _ => Task.CompletedTask, null, "parity skip reason"),
                        new TestCaseDescriptor("Parity", "Excluded", "Parity excluded", _ => throw new InvalidOperationException("must be filtered"), new[] { ExcludedTag }),
                        new TestCaseDescriptor("Parity", "Cancel", "Parity cancel", ct =>
                        {
                            ct.ThrowIfCancellationRequested();
                            return Task.CompletedTask;
                        }),
                    }),
                };
            }
        }

        /// <summary>
        /// Options for the current run: excludes <see cref="ExcludedTag"/> and reads the skip behavior from the
        /// <c>TOUCHSTONE_PARITY_SKIP</c> environment variable (report, omit, or fail; default report).
        /// </summary>
        /// <returns>Run options.</returns>
        public static TestRunOptions Options()
        {
            TestRunOptions options = new TestRunOptions();
            options.Filter.ExcludeTags.Add(ExcludedTag);
            string? mode = Environment.GetEnvironmentVariable("TOUCHSTONE_PARITY_SKIP");
            if (string.Equals(mode, "omit", StringComparison.OrdinalIgnoreCase)) options.SkipBehavior = SkipBehavior.Omit;
            if (string.Equals(mode, "fail", StringComparison.OrdinalIgnoreCase)) options.SkipBehavior = SkipBehavior.Fail;
            return options;
        }

        /// <summary>
        /// The token the host passes for a case: canceled for <see cref="CancelTestId"/>, otherwise the given token.
        /// </summary>
        /// <param name="testId">Test identity.</param>
        /// <param name="token">The host's token.</param>
        /// <returns>The token to execute with.</returns>
        public static CancellationToken TokenFor(string testId, CancellationToken token)
        {
            return testId == CancelTestId ? new CancellationToken(true) : token;
        }

        private static TestCaseDescriptor Scenario(string caseId, string name, bool fail)
        {
            return new ScenarioBuilder<object>("Parity", caseId, name, _ => Task.FromResult(new object()))
                .Given("ready", "a parity context", (_, _) => Task.CompletedTask)
                .Then("check", "the parity expectation holds", (_, _) => fail ? throw new InvalidOperationException("parity scenario failure") : Task.CompletedTask)
                .Build();
        }
    }
}
