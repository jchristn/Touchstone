#nullable enable
namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;
    /// <summary>Immutable feature and requirement metadata, safe for concurrent readers.</summary>
    public sealed class ScenarioMetadata
    {
        /// <summary>Feature description; empty when unspecified.</summary>
        public string Feature { get; }
        /// <summary>Requirement references; never null.</summary>
        public IReadOnlyList<string> Requirements { get; }
        /// <summary>Stable example identity, or null.</summary>
        public string? ExampleId { get; }
        /// <summary>Create metadata. Input collections are copied.</summary>
        /// <exception cref="ArgumentException">A requirement or example identity is empty.</exception>
        public ScenarioMetadata(string feature = "", IReadOnlyList<string>? requirements = null, string? exampleId = null)
        {
            if (exampleId != null && string.IsNullOrWhiteSpace(exampleId))
                throw new ArgumentException("Example identity cannot be blank.", nameof(exampleId));
            List<string> copy = new List<string>();
            foreach (string requirement in requirements ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(requirement)) throw new ArgumentException("Requirement references cannot be blank.", nameof(requirements));
                copy.Add(requirement);
            }
            Feature = feature ?? throw new ArgumentNullException(nameof(feature));
            Requirements = copy.AsReadOnly();
            ExampleId = exampleId;
        }
    }
}
