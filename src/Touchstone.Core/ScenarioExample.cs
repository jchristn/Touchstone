namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// An immutable, named row of typed input for <see cref="ScenarioExamples.Expand{T}"/>.
    /// Thread safety: the row itself is immutable; the thread safety of <see cref="Value"/> is the caller's responsibility.
    /// </summary>
    /// <typeparam name="T">Input type.</typeparam>
    public sealed class ScenarioExample<T>
    {
        private readonly string _ExampleId;
        private readonly T _Value;

        /// <summary>
        /// Create a named example.
        /// </summary>
        /// <param name="exampleId">Stable identity, independent of the value. Cannot be null or blank.</param>
        /// <param name="value">Typed input. May be null for reference types.</param>
        /// <exception cref="ArgumentException">The identity is null or blank.</exception>
        public ScenarioExample(string exampleId, T value)
        {
            if (string.IsNullOrWhiteSpace(exampleId)) throw new ArgumentException("Example identity cannot be blank.", nameof(exampleId));
            _ExampleId = exampleId;
            _Value = value;
        }

        /// <summary>
        /// Stable identity, independent of the value. Never null or blank.
        /// </summary>
        public string ExampleId
        {
            get { return _ExampleId; }
        }

        /// <summary>
        /// Typed input. Values are not captured in reports or telemetry.
        /// </summary>
        public T Value
        {
            get { return _Value; }
        }
    }
}
