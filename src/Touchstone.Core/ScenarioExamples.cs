namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Expands typed example rows into independent cases, one per row. Input values are not captured in identities,
    /// reports, or telemetry.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    public static class ScenarioExamples
    {
        /// <summary>
        /// Build one case per example. The factory assigns each case's identity, usually from the example identity.
        /// </summary>
        /// <typeparam name="T">Input type.</typeparam>
        /// <param name="examples">Rows, in order. Example identities must be unique (ordinal).</param>
        /// <param name="factory">Builds the case for one row. Must not return null; resulting test identities must be unique.</param>
        /// <returns>The cases, in row order.</returns>
        /// <exception cref="ArgumentNullException">The rows, the factory, or a row is null.</exception>
        /// <exception cref="ArgumentException">Example identities or resulting test identities are duplicated, or the factory returned null.</exception>
        public static IReadOnlyList<TestCaseDescriptor> Expand<T>(
            IReadOnlyList<ScenarioExample<T>> examples,
            Func<ScenarioExample<T>, TestCaseDescriptor> factory)
        {
            if (examples == null) throw new ArgumentNullException(nameof(examples));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            HashSet<string> exampleIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> testIds = new HashSet<string>(StringComparer.Ordinal);
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            foreach (ScenarioExample<T>? example in examples)
            {
                if (example == null) throw new ArgumentNullException(nameof(examples), "The example list contains null.");
                if (!exampleIds.Add(example.ExampleId))
                    throw new ArgumentException("Duplicate example identity: " + example.ExampleId, nameof(examples));

                TestCaseDescriptor descriptor = factory(example)
                    ?? throw new ArgumentException("The example factory returned null for " + example.ExampleId + ".", nameof(factory));
                if (!testIds.Add(descriptor.TestId))
                    throw new ArgumentException("Duplicate test identity: " + descriptor.TestId, nameof(factory));

                cases.Add(descriptor);
            }

            return cases.AsReadOnly();
        }
    }
}
