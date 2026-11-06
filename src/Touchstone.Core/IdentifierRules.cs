namespace Touchstone.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validates suite identities, case identities, and tags.
    /// </summary>
    internal static class IdentifierRules
    {
        internal static string SuiteId(string? value, string paramName)
        {
            if (value == null) throw new ArgumentNullException(paramName);
            if (value.Length == 0) throw new ArgumentException("Suite identity cannot be empty.", paramName);

            foreach (char c in value)
            {
                if (!IsIdentityChar(c))
                    throw new ArgumentException(
                        "Suite identity '" + value + "' contains '" + c + "'. Use letters, digits, '_' and '-'. '.' is reserved as the test identity separator.",
                        paramName);
            }

            return value;
        }

        internal static string CaseId(string? value, string paramName)
        {
            if (value == null) throw new ArgumentNullException(paramName);
            if (value.Length == 0) throw new ArgumentException("Case identity cannot be empty.", paramName);

            foreach (char c in value)
            {
                if (!IsIdentityChar(c) && c != '.')
                    throw new ArgumentException(
                        "Case identity '" + value + "' contains '" + c + "'. Use letters, digits, '_', '-' and '.'.",
                        paramName);
            }

            return value;
        }

        internal static string DisplayName(string? value, string paramName)
        {
            if (value == null) throw new ArgumentNullException(paramName);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Display name cannot be blank.", paramName);
            return value;
        }

        internal static string? SkipReason(string? value, string paramName)
        {
            if (value != null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Skip reason cannot be blank. Pass null for a case that is not skipped.", paramName);
            return value;
        }

        internal static string Tag(string? value, string paramName)
        {
            if (value == null) throw new ArgumentException("Tags cannot be null.", paramName);
            if (value.Length == 0) throw new ArgumentException("Tags cannot be empty.", paramName);

            foreach (char c in value)
            {
                if (!IsIdentityChar(c) && c != '.' && c != ':')
                    throw new ArgumentException(
                        "Tag '" + value + "' contains '" + c + "'. Use letters, digits, '_', '-', '.' and ':'.",
                        paramName);
            }

            return value;
        }

        internal static IReadOnlyList<string> Tags(IEnumerable<string>? values, string paramName)
        {
            if (values == null) return Array.Empty<string>();

            List<string> tags = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                string tag = Tag(value, paramName);
                if (seen.Add(tag)) tags.Add(tag);
            }

            return tags.Count == 0 ? Array.Empty<string>() : tags.AsReadOnly();
        }

        private static bool IsIdentityChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
        }
    }
}
