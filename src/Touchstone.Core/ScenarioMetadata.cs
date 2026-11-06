namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable feature and requirement metadata for a scenario.
    /// Thread safety: safe to read concurrently.
    /// </summary>
    public sealed class ScenarioMetadata
    {
        private readonly string _Feature;
        private readonly IReadOnlyList<string> _Requirements;
        private readonly string? _ExampleId;

        /// <summary>
        /// Create metadata. The requirement list is copied.
        /// </summary>
        /// <param name="feature">Feature description. Default empty, meaning unspecified. Cannot be null.</param>
        /// <param name="requirements">Requirement references, such as ticket or specification identifiers. Null means none. Entries cannot be blank.</param>
        /// <param name="exampleId">Stable example identity for a case expanded from an example row. Null when not from an example; cannot be blank.</param>
        /// <exception cref="ArgumentNullException">The feature is null.</exception>
        /// <exception cref="ArgumentException">A requirement or the example identity is blank.</exception>
        public ScenarioMetadata(string feature = "", IReadOnlyList<string>? requirements = null, string? exampleId = null)
        {
            _Feature = feature ?? throw new ArgumentNullException(nameof(feature));
            if (exampleId != null && string.IsNullOrWhiteSpace(exampleId))
                throw new ArgumentException("Example identity cannot be blank.", nameof(exampleId));

            List<string> copy = new List<string>();
            foreach (string requirement in requirements ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(requirement))
                    throw new ArgumentException("Requirement references cannot be blank.", nameof(requirements));
                copy.Add(requirement);
            }

            _Requirements = copy.AsReadOnly();
            _ExampleId = exampleId;
        }

        /// <summary>
        /// Feature description. Empty when unspecified; never null.
        /// </summary>
        public string Feature
        {
            get { return _Feature; }
        }

        /// <summary>
        /// Requirement references. Never null.
        /// </summary>
        public IReadOnlyList<string> Requirements
        {
            get { return _Requirements; }
        }

        /// <summary>
        /// Stable example identity. Null when the case did not come from an example row.
        /// </summary>
        public string? ExampleId
        {
            get { return _ExampleId; }
        }
    }
}
