namespace Touchstone.MstestAdapter
{
    using System;
    using System.Runtime.Serialization;

    using Touchstone.Core;

    /// <summary>
    /// Serializable key for one Touchstone case in an MSTest data row. MSTest serializes row data at discovery, so a
    /// key (rather than the descriptor) lets Test Explorer, <c>--list-tests</c>, and category filters see one test per
    /// case. <see cref="TouchstoneMstest.ExecuteAsync"/> resolves the key back to the case.
    /// Thread safety: immutable.
    /// </summary>
    [DataContract]
    public sealed class TouchstoneMstestCase
    {
        [DataMember(Name = "provider")]
        private string _ProviderType = string.Empty;

        [DataMember(Name = "testId")]
        private string _TestId = string.Empty;

        [DataMember(Name = "displayName")]
        private string _DisplayName = string.Empty;

        [DataMember(Name = "skipReason")]
        private string? _SkipReason = null;

        [DataMember(Name = "skipBehavior")]
        private int _SkipBehavior = 0;

        internal TouchstoneMstestCase(Type providerType, TestCaseDescriptor testCase, SkipBehavior skipBehavior)
        {
            _ProviderType = providerType.AssemblyQualifiedName ?? providerType.FullName ?? providerType.Name;
            _TestId = testCase.TestId;
            _DisplayName = testCase.DisplayName;
            _SkipReason = testCase.SkipReason;
            _SkipBehavior = (int)skipBehavior;
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
            get { return (SkipBehavior)_SkipBehavior; }
        }

        /// <summary>
        /// Returns the display name.
        /// </summary>
        /// <returns>The display name.</returns>
        public override string ToString()
        {
            return _DisplayName;
        }
    }
}
