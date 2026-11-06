namespace Touchstone.Xunit3Adapter
{
    using System;
    using System.Collections.Generic;

    using Touchstone.Adapters;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// Creates xUnit v3 theory data with one row per selected case. Each row carries the case's display name, its tags
    /// as <c>Category</c> traits, and, under <see cref="SkipBehavior.Report"/>, its skip reason as a native row skip.
    /// Use with <c>[MemberData]</c> and run each row through <see cref="TouchstoneXunit3.ExecuteAsync"/>.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneXunit3Cases
    {
        /// <summary>
        /// Trait name used for case tags.
        /// </summary>
        public const string TagTraitName = "Category";

        /// <summary>
        /// Create one row per case supplied by <typeparamref name="TProvider"/>, applying the filter and skip behavior.
        /// Under <see cref="SkipBehavior.Omit"/>, skipped cases get no row; under <see cref="SkipBehavior.Fail"/>, their row fails.
        /// </summary>
        /// <typeparam name="TProvider">Suite provider with a public parameterless constructor.</typeparam>
        /// <param name="options">Filter, skip behavior, and case timeout. Each row carries the skip behavior and case timeout and applies them when it runs. Other settings, including MaxDegreeOfParallelism, are ignored: use the framework's own parallelization. Null uses the defaults.</param>
        /// <returns>Theory data, in suite and case order.</returns>
        /// <exception cref="ArgumentException">The provider's suites contain null, or the filter contains an invalid value.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        /// <exception cref="InvalidOperationException">The provider cannot be created or returns null suites.</exception>
        public static TheoryData<TouchstoneXunit3Case> From<TProvider>(TestRunOptions? options = null)
            where TProvider : ITestSuiteProvider, new()
        {
            TestRunOptions settings = options ?? new TestRunOptions();
            settings.Filter.Validate();

            TheoryData<TouchstoneXunit3Case> data = new TheoryData<TouchstoneXunit3Case>();
            foreach (TestCaseDescriptor testCase in CaseIndex.Flatten(CaseIndex.Suites(typeof(TProvider)), "suites"))
            {
                if (!settings.Filter.Matches(testCase)) continue;
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Omit) continue;

                TheoryDataRow<TouchstoneXunit3Case> row = new TheoryDataRow<TouchstoneXunit3Case>(
                    new TouchstoneXunit3Case(typeof(TProvider), testCase, settings.SkipBehavior, settings.CaseTimeout));
                row = row.WithTestDisplayName(testCase.DisplayName);
                foreach (string tag in testCase.Tags) row = row.WithTrait(TagTraitName, tag);
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Report) row = row.WithSkip(testCase.SkipReason);
                data.Add(row);
            }

            return data;
        }
    }
}
