#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    /// <summary>Expands typed example rows into independently discoverable cases. Does not capture input values.</summary>
    public static class ScenarioExamples
    {
        /// <summary>Build cases using a factory that assigns deterministic case identities from example identities.</summary>
        /// <exception cref="ArgumentNullException">An argument or row is null.</exception>
        /// <exception cref="ArgumentException">Example identities or resulting test identities are duplicated.</exception>
        public static IReadOnlyList<TestCaseDescriptor> Expand<T>(IReadOnlyList<ScenarioExample<T>> examples,
            Func<ScenarioExample<T>, TestCaseDescriptor> factory)
        {
            ArgumentNullException.ThrowIfNull(examples);
            ArgumentNullException.ThrowIfNull(factory);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> tests = new HashSet<string>(StringComparer.Ordinal);
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            foreach (ScenarioExample<T> example in examples)
            {
                ArgumentNullException.ThrowIfNull(example);
                if (!ids.Add(example.ExampleId)) throw new ArgumentException("Duplicate example identity: " + example.ExampleId, nameof(examples));
                TestCaseDescriptor descriptor = factory(example) ?? throw new ArgumentException("Example factory returned null.", nameof(factory));
                if (!tests.Add(descriptor.TestId)) throw new ArgumentException("Duplicate test identity: " + descriptor.TestId, nameof(factory));
                cases.Add(descriptor);
            }
            return cases.AsReadOnly();
        }
    }
}
