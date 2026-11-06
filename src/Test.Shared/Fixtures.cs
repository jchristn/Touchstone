namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Builders for descriptors used as inputs by the contract suites.
    /// </summary>
    public static class Fixtures
    {
        /// <summary>
        /// A passing case.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="caseId">Case identity.</param>
        /// <param name="onExecute">Optional callback run inside the case body.</param>
        /// <param name="tags">Optional tags.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Passing(string suiteId, string caseId, Action<CancellationToken>? onExecute = null, IReadOnlyList<string>? tags = null)
        {
            return new TestCaseDescriptor(suiteId, caseId, caseId + " display", ct =>
            {
                onExecute?.Invoke(ct);
                return Task.CompletedTask;
            }, tags);
        }

        /// <summary>
        /// A case that throws <see cref="InvalidOperationException"/> with the given message.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="caseId">Case identity.</param>
        /// <param name="message">Exception message.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Failing(string suiteId, string caseId, string message = "boom")
        {
            return new TestCaseDescriptor(suiteId, caseId, caseId + " display", _ => throw new InvalidOperationException(message));
        }

        /// <summary>
        /// A skipped case whose body records that it ran.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="caseId">Case identity.</param>
        /// <param name="ran">Set when the body runs.</param>
        /// <param name="reason">Skip reason.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Skipped(string suiteId, string caseId, StrongBox<bool>? ran = null, string reason = "not ready")
        {
            return new TestCaseDescriptor(suiteId, caseId, caseId + " display", _ =>
            {
                if (ran != null) ran.Value = true;
                return Task.CompletedTask;
            }, null, reason);
        }

        /// <summary>
        /// A case that waits for cancellation and then observes it.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="caseId">Case identity.</param>
        /// <param name="source">Source the case cancels before observing its token.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor CancelsRun(string suiteId, string caseId, CancellationTokenSource source)
        {
            return new TestCaseDescriptor(suiteId, caseId, caseId + " display", ct =>
            {
                source.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// A suite of the given cases.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="cases">Cases, which must use <paramref name="suiteId"/>.</param>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor Suite(string suiteId, params TestCaseDescriptor[] cases)
        {
            return new TestSuiteDescriptor(suiteId, suiteId + " display", cases);
        }

        /// <summary>
        /// A suite with hooks.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="before">Setup hook, or null.</param>
        /// <param name="after">Teardown hook, or null.</param>
        /// <param name="cases">Cases.</param>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor HookedSuite(
            string suiteId,
            Func<CancellationToken, ValueTask>? before,
            Func<CancellationToken, ValueTask>? after,
            params TestCaseDescriptor[] cases)
        {
            return new TestSuiteDescriptor(suiteId, suiteId + " display", cases, before, after);
        }

        /// <summary>
        /// A one-step scenario over <see cref="ScenarioContext"/>.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="caseId">Case identity.</param>
        /// <param name="step">Step body.</param>
        /// <param name="contexts">Receives every context created.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Scenario(
            string suiteId,
            string caseId,
            Func<ScenarioContext, CancellationToken, Task> step,
            List<ScenarioContext>? contexts = null)
        {
            return new ScenarioBuilder<ScenarioContext>(suiteId, caseId, caseId + " display", ct =>
                {
                    ScenarioContext context = new ScenarioContext();
                    contexts?.Add(context);
                    return Task.FromResult(context);
                })
                .When("act", "the action runs", step)
                .Build();
        }
    }
}
