namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Exposes the protected members of <see cref="TouchstoneMstestBase"/> so shared suites can exercise them directly.
    /// </summary>
    public sealed class MstestBaseHarness : TouchstoneMstestBase
    {
        private readonly IReadOnlyList<TestSuiteDescriptor> _Suites;

        /// <summary>
        /// Initialize the harness with the suites to expose.
        /// </summary>
        /// <param name="suites">Suites returned by the Suites property.</param>
        public MstestBaseHarness(IReadOnlyList<TestSuiteDescriptor> suites)
        {
            _Suites = suites ?? throw new ArgumentNullException(nameof(suites));
        }

        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return _Suites; }
        }

        /// <summary>
        /// Invoke the protected RunAllAsync method.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token forwarded to RunAllAsync.</param>
        /// <returns>Task that completes when RunAllAsync completes.</returns>
        public Task InvokeRunAllAsync(CancellationToken cancellationToken)
        {
            return RunAllAsync(cancellationToken);
        }
    }
}
