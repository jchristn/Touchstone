namespace Test.Shared.Xunit3
{
    using System;
    using System.Collections.Generic;

    using Xunit.Sdk;

    /// <summary>
    /// Dictionary-backed serialization info used to build keys with chosen values.
    /// </summary>
    public sealed class FakeSerializationInfo : IXunitSerializationInfo
    {
        private readonly Dictionary<string, object?> _Values = new Dictionary<string, object?>(StringComparer.Ordinal);

        /// <inheritdoc />
        public void AddValue(string key, object? value, Type? valueType = null)
        {
            _Values[key] = value;
        }

        /// <inheritdoc />
        public object? GetValue(string key)
        {
            return _Values.TryGetValue(key, out object? value) ? value : null;
        }
    }
}
