namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Shorthand for building the shared test case descriptors.
    /// </summary>
    public static class CaseBuilder
    {
        /// <summary>
        /// Build an asynchronous test case.
        /// </summary>
        /// <param name="suiteId">Parent suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="displayName">Human-readable name.</param>
        /// <param name="body">Asynchronous test body.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor Async(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, body);
        }

        /// <summary>
        /// Build a synchronous test case.
        /// </summary>
        /// <param name="suiteId">Parent suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        /// <param name="displayName">Human-readable name.</param>
        /// <param name="body">Synchronous test body.</param>
        /// <returns>Test case descriptor.</returns>
        public static TestCaseDescriptor Sync(string suiteId, string caseId, string displayName, Action body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            return new TestCaseDescriptor(suiteId, caseId, displayName, _ =>
            {
                body();
                return Task.CompletedTask;
            });
        }
    }
}
