namespace Touchstone.Xunit2Adapter
{
    using System;
    using System.ComponentModel;

    using Touchstone.Core;
    using Xunit.Abstractions;

    /// <summary>
    /// Serializable key for one Touchstone case in an xUnit v2 theory row. xUnit serializes the key at discovery and
    /// <see cref="TouchstoneXunit2.ExecuteAsync"/> resolves it back to the case, so Test Explorer lists one entry per case.
    /// Thread safety: immutable after deserialization.
    /// </summary>
    public sealed class TouchstoneXunit2Case : IXunitSerializable
    {
        private string _ProviderType = string.Empty;
        private string _TestId = string.Empty;
        private string _DisplayName = string.Empty;
        private string? _SkipReason = null;
        private SkipBehavior _SkipBehavior = SkipBehavior.Report;
        private long _CaseTimeoutTicks = 0;

        /// <summary>
        /// Initialize an empty key. Used by xUnit deserialization; not for direct use.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TouchstoneXunit2Case()
        {
        }

        internal TouchstoneXunit2Case(Type providerType, TestCaseDescriptor testCase, SkipBehavior skipBehavior, TimeSpan? caseTimeout)
        {
            _ProviderType = providerType.AssemblyQualifiedName ?? providerType.FullName ?? providerType.Name;
            _TestId = testCase.TestId;
            _DisplayName = testCase.DisplayName;
            _SkipReason = testCase.SkipReason;
            _SkipBehavior = skipBehavior;
            _CaseTimeoutTicks = caseTimeout.HasValue ? caseTimeout.Value.Ticks : 0;
        }

        /// <summary>
        /// Test identity of the case, <c>SuiteId.CaseId</c>.
        /// </summary>
        public string TestId
        {
            get { return _TestId; }
        }

        /// <summary>
        /// Display name of the case.
        /// </summary>
        public string DisplayName
        {
            get { return _DisplayName; }
        }

        /// <summary>
        /// Skip reason of the case. Null when the case is not skipped.
        /// </summary>
        public string? SkipReason
        {
            get { return _SkipReason; }
        }

        internal string ProviderType
        {
            get { return _ProviderType; }
        }

        internal SkipBehavior SkipBehavior
        {
            get { return _SkipBehavior; }
        }

        internal TimeSpan? CaseTimeout
        {
            get { return _CaseTimeoutTicks > 0 ? TimeSpan.FromTicks(_CaseTimeoutTicks) : null; }
        }

        /// <inheritdoc />
        public void Deserialize(IXunitSerializationInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            _ProviderType = info.GetValue<string>("provider") ?? string.Empty;
            _TestId = info.GetValue<string>("testId") ?? string.Empty;
            _DisplayName = info.GetValue<string>("displayName") ?? string.Empty;
            _SkipReason = info.GetValue<string>("skipReason");
            _SkipBehavior = (SkipBehavior)info.GetValue<int>("skipBehavior");
            _CaseTimeoutTicks = info.GetValue<long>("caseTimeoutTicks");
        }

        /// <inheritdoc />
        public void Serialize(IXunitSerializationInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            info.AddValue("provider", _ProviderType);
            info.AddValue("testId", _TestId);
            info.AddValue("displayName", _DisplayName);
            info.AddValue("skipReason", _SkipReason);
            info.AddValue("skipBehavior", (int)_SkipBehavior);
            info.AddValue("caseTimeoutTicks", _CaseTimeoutTicks);
        }

        /// <summary>
        /// Returns the display name, which xUnit shows in the test name.
        /// </summary>
        /// <returns>The display name.</returns>
        public override string ToString()
        {
            return _DisplayName;
        }
    }
}
