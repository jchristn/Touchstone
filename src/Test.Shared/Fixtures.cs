namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Builders for descriptors used as inputs to the code under test.
    /// </summary>
    public static class Fixtures
    {
        /// <summary>
        /// Build a case that completes successfully.
        /// </summary>
        /// <param name="suiteId">Parent suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="onExecute">Optional callback invoked when the case runs.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor Passing(string suiteId, string caseId, Action<CancellationToken>? onExecute = null)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: caseId + " display",
                executeAsync: ct =>
                {
                    onExecute?.Invoke(ct);
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Build a case that throws <see cref="InvalidOperationException"/> with the supplied message.
        /// </summary>
        /// <param name="suiteId">Parent suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="message">Exception message.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor Failing(string suiteId, string caseId, string message)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: caseId + " display",
                executeAsync: _ => throw new InvalidOperationException(message));
        }

        /// <summary>
        /// Build a skipped case whose delegate throws if it is ever invoked.
        /// </summary>
        /// <param name="suiteId">Parent suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="skipReason">Optional skip reason.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor Skipped(string suiteId, string caseId, string? skipReason = "not ready")
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: caseId + " display",
                executeAsync: _ => throw new InvalidOperationException("Skipped case " + caseId + " must not execute"),
                skip: true,
                skipReason: skipReason);
        }

        /// <summary>
        /// Build a suite from the supplied cases.
        /// </summary>
        /// <param name="suiteId">Suite identifier.</param>
        /// <param name="cases">Cases in the suite.</param>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite(string suiteId, params TestCaseDescriptor[] cases)
        {
            return new TestSuiteDescriptor(suiteId, suiteId + " display", new List<TestCaseDescriptor>(cases));
        }
    }
}
