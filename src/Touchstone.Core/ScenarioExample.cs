#nullable enable
namespace Touchstone.Core
{
    using System;
    /// <summary>Immutable named data row. Consumers own thread safety of the supplied value.</summary>
    public sealed class ScenarioExample<T>
    {
        /// <summary>Stable identity, independent of parameter contents.</summary>
        public string ExampleId { get; }
        /// <summary>Typed input. Values are not automatically captured in reports or telemetry.</summary>
        public T Value { get; }
        /// <summary>Create a named example.</summary>
        /// <exception cref="ArgumentException">Identity is blank.</exception>
        public ScenarioExample(string exampleId, T value)
        {
            if (string.IsNullOrWhiteSpace(exampleId)) throw new ArgumentException("Example identity cannot be blank.", nameof(exampleId));
            ExampleId = exampleId;
            Value = value;
        }
    }
}
