namespace Test.Shared
{
    using System.Collections.Generic;

    /// <summary>
    /// A framework-neutral view of one native data row.
    /// </summary>
    public sealed class AdapterRow
    {
        /// <summary>
        /// Initialize a row view.
        /// </summary>
        /// <param name="testId">Test identity behind the row.</param>
        /// <param name="displayName">Native display name.</param>
        /// <param name="skipReason">Native row-level skip reason, or null.</param>
        /// <param name="categories">Native categories or traits.</param>
        /// <param name="native">The native row object.</param>
        public AdapterRow(string testId, string displayName, string? skipReason, IReadOnlyList<string> categories, object native)
        {
            TestId = testId;
            DisplayName = displayName;
            SkipReason = skipReason;
            Categories = categories;
            Native = native;
        }

        /// <summary>
        /// Test identity behind the row.
        /// </summary>
        public string TestId { get; }

        /// <summary>
        /// Native display name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Native row-level skip reason, or null.
        /// </summary>
        public string? SkipReason { get; }

        /// <summary>
        /// Native categories or traits.
        /// </summary>
        public IReadOnlyList<string> Categories { get; }

        /// <summary>
        /// The native row object.
        /// </summary>
        public object Native { get; }
    }
}
