namespace Touchstone.Cli
{
    using System;

    /// <summary>
    /// Thrown when console runner arguments are invalid.
    /// Thread safety: instances are immutable after construction.
    /// </summary>
    public sealed class ConsoleArgumentException : ArgumentException
    {
        /// <summary>
        /// Initialize a new instance with a default message.
        /// </summary>
        public ConsoleArgumentException()
            : base("The console runner arguments are invalid.")
        {
        }

        /// <summary>
        /// Initialize a new instance with a message.
        /// </summary>
        /// <param name="message">Error message, naming the offending argument.</param>
        public ConsoleArgumentException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initialize a new instance with a message and an inner exception.
        /// </summary>
        /// <param name="message">Error message, naming the offending argument.</param>
        /// <param name="innerException">Exception that caused this one, or null.</param>
        public ConsoleArgumentException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
