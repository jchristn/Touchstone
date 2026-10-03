namespace Test.Shared
{
    using System;

    /// <summary>
    /// Thrown when an assertion inside a shared test case fails.
    /// </summary>
    public sealed class TestAssertionException : Exception
    {
        /// <summary>
        /// Initialize a new assertion exception.
        /// </summary>
        /// <param name="message">Description of the failed assertion.</param>
        public TestAssertionException(string message)
            : base(message)
        {
        }
    }
}
