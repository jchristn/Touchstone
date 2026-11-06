namespace Test.Shared.Xunit2
{
    using System.Globalization;
    using System.Text;

    using Xunit.Abstractions;

    /// <summary>
    /// Output helper that records everything written to it.
    /// </summary>
    public sealed class CapturingOutput : ITestOutputHelper
    {
        private readonly StringBuilder _Text = new StringBuilder();

        /// <summary>
        /// Everything written so far.
        /// </summary>
        public string Text
        {
            get { return _Text.ToString(); }
        }

        /// <inheritdoc />
        public void WriteLine(string message)
        {
            _Text.AppendLine(message);
        }

        /// <inheritdoc />
        public void WriteLine(string format, params object[] args)
        {
            _Text.AppendLine(string.Format(CultureInfo.InvariantCulture, format, args));
        }
    }
}
