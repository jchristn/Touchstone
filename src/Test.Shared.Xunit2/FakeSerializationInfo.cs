namespace Test.Shared.Xunit2
{
    using System;
    using System.Collections.Generic;

    using Xunit.Abstractions;

    /// <summary>
    /// Dictionary-backed serialization info used to build keys with chosen values.
    /// </summary>
    public sealed class FakeSerializationInfo : IXunitSerializationInfo
    {
        private readonly Dictionary<string, object?> _Values = new Dictionary<string, object?>(StringComparer.Ordinal);

        /// <inheritdoc />
        public void AddValue(string key, object? value, Type? type = null)
        {
            _Values[key] = value;
        }

        /// <inheritdoc />
        public object? GetValue(string key, Type type)
        {
            return _Values.TryGetValue(key, out object? value) ? value : null;
        }

        /// <inheritdoc />
        public T GetValue<T>(string key)
        {
            return _Values.TryGetValue(key, out object? value) && value != null ? (T)value : default!;
        }
    }
}
