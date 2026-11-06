namespace Touchstone.Core
{
    using System;

    /// <summary>
    /// Thrown when two cases or two suites share an identity.
    /// Thread safety: instances are immutable after construction.
    /// </summary>
    public sealed class DuplicateTestIdException : ArgumentException
    {
        /// <summary>
        /// Initialize a new instance with a default message.
        /// </summary>
        public DuplicateTestIdException()
            : base("A test or suite identity is duplicated.")
        {
            DuplicateId = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance with a message.
        /// </summary>
        /// <param name="message">Error message.</param>
        public DuplicateTestIdException(string message)
            : base(message)
        {
            DuplicateId = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance with a message and an inner exception.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Exception that caused this one, or null.</param>
        public DuplicateTestIdException(string message, Exception? innerException)
            : base(message, innerException)
        {
            DuplicateId = string.Empty;
        }

        /// <summary>
        /// Initialize a new instance for a specific duplicated identity.
        /// </summary>
        /// <param name="duplicateId">The duplicated test or suite identity.</param>
        /// <param name="message">Error message.</param>
        /// <param name="paramName">Name of the argument that contained the duplicate.</param>
        public DuplicateTestIdException(string duplicateId, string message, string? paramName)
            : base(message, paramName)
        {
            DuplicateId = duplicateId ?? string.Empty;
        }

        /// <summary>
        /// The duplicated test identity (for cases) or suite identity (for suites). Empty when not supplied.
        /// </summary>
        public string DuplicateId { get; }
    }
}
