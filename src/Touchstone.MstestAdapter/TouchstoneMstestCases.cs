namespace Touchstone.MstestAdapter
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Touchstone.Adapters;
    using Touchstone.Core;

    /// <summary>
    /// Creates MSTest data rows with one row per selected case. Each row carries the case's display name, its tags as
    /// test categories, and, under <see cref="SkipBehavior.Report"/>, its skip reason as a native ignore message.
    /// Use with <c>[DynamicData]</c> and run each row through <see cref="TouchstoneMstest.ExecuteAsync"/>.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneMstestCases
    {
        /// <summary>
        /// Create one row per case supplied by <typeparamref name="TProvider"/>, applying the filter and skip behavior.
        /// Under <see cref="SkipBehavior.Omit"/>, skipped cases get no row; under <see cref="SkipBehavior.Fail"/>, their
        /// row fails.
        /// </summary>
        /// <typeparam name="TProvider">Suite provider with a public parameterless constructor.</typeparam>
        /// <param name="options">Filter, skip behavior, and case timeout. Each row carries the skip behavior and case timeout and applies them when it runs. Other settings, including MaxDegreeOfParallelism, are ignored: use the framework's own parallelization. Null uses the defaults.</param>
        /// <returns>Data rows, in suite and case order.</returns>
        /// <exception cref="ArgumentException">The provider's suites contain null, or the filter contains an invalid value.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        /// <exception cref="InvalidOperationException">The provider cannot be created or returns null suites.</exception>
        public static IReadOnlyList<TestDataRow<TouchstoneMstestCase>> From<TProvider>(TestRunOptions? options = null)
            where TProvider : ITestSuiteProvider, new()
        {
            TestRunOptions settings = options ?? new TestRunOptions();
            settings.Filter.Validate();

            List<TestDataRow<TouchstoneMstestCase>> rows = new List<TestDataRow<TouchstoneMstestCase>>();
            foreach (TestCaseDescriptor testCase in CaseIndex.Flatten(CaseIndex.Suites(typeof(TProvider)), "suites"))
            {
                if (!settings.Filter.Matches(testCase)) continue;
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Omit) continue;

                TestDataRow<TouchstoneMstestCase> row = new TestDataRow<TouchstoneMstestCase>(
                    new TouchstoneMstestCase(typeof(TProvider), testCase, settings.SkipBehavior, settings.CaseTimeout))
                {
                    DisplayName = testCase.DisplayName,
                };
                if (testCase.Tags.Count > 0) row.TestCategories = new List<string>(testCase.Tags);
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Report) row.IgnoreMessage = testCase.SkipReason;
                rows.Add(row);
            }

            return rows.AsReadOnly();
        }
    }
}
