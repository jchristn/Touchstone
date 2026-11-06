namespace Smoke.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// A consumer-style shared suite that references only Touchstone.Core.
    /// </summary>
    public sealed class SmokeSuites : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get
            {
                return new[]
                {
                    new TestSuiteDescriptor("Smoke", "Package smoke", new[]
                    {
                        new TestCaseDescriptor("Smoke", "Adds", "Addition works", _ =>
                        {
                            if (1 + 1 != 2) throw new InvalidOperationException("math");
                            return Task.CompletedTask;
                        }, new[] { "smoke" }),
                        new ScenarioBuilder<List<int>>("Smoke", "Scenario", "A list grows", _ => Task.FromResult(new List<int>()))
                            .When("add", "an item is added", (list, _) => { list.Add(1); return Task.CompletedTask; })
                            .Then("count", "the list has one item", (list, _) => list.Count == 1 ? Task.CompletedTask : throw new InvalidOperationException("count"))
                            .Build(),
                        new TestCaseDescriptor("Smoke", "Later", "Not ready yet", _ => Task.CompletedTask, null, "not implemented"),
                    }),
                };
            }
        }
    }
}
