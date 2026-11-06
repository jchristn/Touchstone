namespace Test.Shared.Mstest
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test context that records what the adapter writes and forwards it to the real context when there is one.
    /// </summary>
    public sealed class CapturingTestContext : TestContext
    {
        private readonly TestContext? _Inner;
        private readonly StringBuilder _Text = new StringBuilder();
        private readonly Dictionary<string, object?> _Properties = new Dictionary<string, object?>();

        /// <summary>
        /// Initialize the capture.
        /// </summary>
        /// <param name="inner">Real context to forward to, or null.</param>
        public CapturingTestContext(TestContext? inner)
        {
            _Inner = inner;
        }

        /// <summary>
        /// Everything written so far.
        /// </summary>
        public string Text
        {
            get { return _Text.ToString(); }
        }

        /// <inheritdoc />
        public override IDictionary<string, object?> Properties
        {
            get { return _Properties; }
        }

        /// <inheritdoc />
        public override void AddResultFile(string fileName)
        {
            _Inner?.AddResultFile(fileName);
        }

        /// <inheritdoc />
        public override void DisplayMessage(MessageLevel messageLevel, string message)
        {
            _Inner?.DisplayMessage(messageLevel, message);
        }

        /// <inheritdoc />
        public override void Write(string? message)
        {
            _Text.Append(message);
            _Inner?.Write(message);
        }

        /// <inheritdoc />
        public override void Write(string format, params object?[] args)
        {
            Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        }

        /// <inheritdoc />
        public override void WriteLine(string? message)
        {
            _Text.AppendLine(message);
            _Inner?.WriteLine(message);
        }

        /// <inheritdoc />
        public override void WriteLine(string format, params object?[] args)
        {
            WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
        }
    }
}
