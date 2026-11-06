namespace Touchstone.NunitAdapter
{
    using System;
    using System.Collections.Generic;

    using NUnit.Framework;
    using Touchstone.Adapters;
    using Touchstone.Core;

    /// <summary>
    /// Creates NUnit test case data with one entry per selected case. Each entry carries the case's display name as
    /// the test name, its tags as categories, and, under <see cref="SkipBehavior.Report"/>, its skip reason as a native
    /// ignore. Use with <c>[TestCaseSource]</c> and run each entry through <see cref="TouchstoneNunit.ExecuteAsync"/>.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class TouchstoneNunitCases
    {
        /// <summary>
        /// Create one entry per selected case, applying the filter and skip behavior. The entry's single argument is the
        /// <see cref="TestCaseDescriptor"/>. Under <see cref="SkipBehavior.Omit"/>, skipped cases get no entry; under
        /// <see cref="SkipBehavior.Fail"/>, their entry fails when it runs with the same options.
        /// </summary>
        /// <param name="suites">Suites to enumerate. Suite identities must be unique.</param>
        /// <param name="options">Filter and skip behavior. Null uses the defaults.</param>
        /// <returns>Test case data, in suite and case order.</returns>
        /// <exception cref="ArgumentNullException">The suite list is null.</exception>
        /// <exception cref="ArgumentException">The suite list contains null, or the filter contains an invalid value.</exception>
        /// <exception cref="DuplicateTestIdException">Two suites share an identity.</exception>
        public static IReadOnlyList<TestCaseData> From(IReadOnlyList<TestSuiteDescriptor> suites, TestRunOptions? options = null)
        {
            IReadOnlyList<TestCaseDescriptor> cases = CaseIndex.Flatten(suites, nameof(suites));
            TestRunOptions settings = options ?? new TestRunOptions();
            settings.Filter.Validate();

            List<TestCaseData> data = new List<TestCaseData>();
            foreach (TestCaseDescriptor testCase in cases)
            {
                if (!settings.Filter.Matches(testCase)) continue;
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Omit) continue;

                TestCaseData entry = new TestCaseData(testCase).SetName(testCase.DisplayName);
                foreach (string tag in testCase.Tags) entry = entry.SetCategory(tag);
                if (testCase.IsSkipped && settings.SkipBehavior == SkipBehavior.Report) entry = entry.Ignore(testCase.SkipReason!);
                data.Add(entry);
            }

            return data.AsReadOnly();
        }
    }
}
