namespace Touchstone.Xunit2Adapter
{
    using System;
    using System.Collections.Generic;

    using Touchstone.Adapters;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// Creates xUnit v2 theory data with one row per selected case.
    /// Use with <c>[MemberData]</c> and run each row through <see cref="TouchstoneXunit2.ExecuteAsync"/>.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneXunit2Cases
    {
        /// <summary>
        /// Create one row per case supplied by <typeparamref name="TProvider"/>, applying the filter and skip behavior.
        /// Under <see cref="SkipBehavior.Report"/>, skipped cases get a row that passes and writes the skip reason to the
        /// output, because xUnit v2 cannot skip a theory row at run time. Under <see cref="SkipBehavior.Omit"/> they get
        /// no row; under <see cref="SkipBehavior.Fail"/> their row fails.
        /// </summary>
        /// <typeparam name="TProvider">Suite provider with a public parameterless constructor.</typeparam>
        /// <param name="options">Filter, skip behavior, and case timeout. Each row carries the skip behavior and case timeout and applies them when it runs. Other settings, including MaxDegreeOfParallelism, are ignored: use the framework's own parallelization. Null uses the defaults.</param>
        /// <returns>Theory data, in suite and case order.</returns>
        /// <exception cref="ArgumentException">The provider's suites contain null, or the filter contains an invalid value.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        /// <exception cref="InvalidOperationException">The provider cannot be created or returns null suites.</exception>
        public static TheoryData<TouchstoneXunit2Case> From<TProvider>(TestRunOptions? options = null)
            where TProvider : ITestSuiteProvider, new()
        {
            TestRunOptions settings = options ?? new TestRunOptions();
            settings.Filter.Validate();

            TheoryData<TouchstoneXunit2Case> data = new TheoryData<TouchstoneXunit2Case>();
            foreach (TestCaseDescriptor testCase in CaseIndex.Flatten(CaseIndex.Suites(typeof(TProvider)), "suites"))
            {
                if (!settings.Filter.Matches(testCase)) continue;
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Omit) continue;
                data.Add(new TouchstoneXunit2Case(typeof(TProvider), testCase, settings.SkipBehavior, settings.CaseTimeout));
            }

            return data;
        }
    }
}
