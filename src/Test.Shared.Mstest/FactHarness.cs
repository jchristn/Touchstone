namespace Test.Shared.Mstest
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.MstestAdapter;

    /// <summary>
    /// Exposes <see cref="TouchstoneMstestFactBase.RunAllAsync"/> for the contract suites.
    /// </summary>
    public sealed class FactHarness : TouchstoneMstestFactBase
    {
        private readonly IReadOnlyList<TestSuiteDescriptor> _Suites;
        private readonly TestRunOptions? _Options;

        /// <summary>
        /// Initialize the harness.
        /// </summary>
        /// <param name="suites">Suites to run.</param>
        /// <param name="options">Options, or null.</param>
        public FactHarness(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options)
        {
            _Suites = suites;
            _Options = options;
        }

        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return _Suites; }
        }

        /// <inheritdoc />
        protected override TestRunOptions? Options
        {
            get { return _Options; }
        }

        /// <summary>
        /// Invoke the protected run-all method.
        /// </summary>
        /// <param name="cancellationToken">Caller cancellation.</param>
        /// <returns>The run-all task.</returns>
        public Task InvokeAsync(CancellationToken cancellationToken)
        {
            return RunAllAsync(cancellationToken);
        }
    }
}
