namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Builds catalog cases. The case identity is the catalog ID, so results can be checked against archive/V1.0_PLAN.md section 11.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// An asynchronous catalog case.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="catalogId">Catalog ID, for example <c>DESC-01</c>.</param>
        /// <param name="name">What the case verifies.</param>
        /// <param name="body">Case body. Throws <see cref="TestAssertionException"/> on failure.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Async(string suiteId, string catalogId, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(suiteId, catalogId, catalogId + " " + name, body);
        }

        /// <summary>
        /// A synchronous catalog case.
        /// </summary>
        /// <param name="suiteId">Suite identity.</param>
        /// <param name="catalogId">Catalog ID, for example <c>DESC-01</c>.</param>
        /// <param name="name">What the case verifies.</param>
        /// <param name="body">Case body. Throws <see cref="TestAssertionException"/> on failure.</param>
        /// <returns>The descriptor.</returns>
        public static TestCaseDescriptor Sync(string suiteId, string catalogId, string name, Action body)
        {
            return new TestCaseDescriptor(suiteId, catalogId, catalogId + " " + name, ct =>
            {
                ct.ThrowIfCancellationRequested();
                body();
                return Task.CompletedTask;
            });
        }
    }
}
