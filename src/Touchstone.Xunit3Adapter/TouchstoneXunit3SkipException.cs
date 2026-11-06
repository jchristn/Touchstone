namespace Touchstone.Xunit3Adapter
{
    using System;

    using Xunit.v3;

    /// <summary>
    /// Signals a dynamic skip to xUnit v3. xUnit treats an exception whose message starts with
    /// <see cref="DynamicSkipToken.Value"/> as a skip with the rest of the message as the reason.
    /// </summary>
    internal sealed class TouchstoneXunit3SkipException : Exception
    {
        internal TouchstoneXunit3SkipException(string reason)
            : base(DynamicSkipToken.Value + reason)
        {
        }
    }
}
