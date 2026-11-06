namespace Touchstone.Cli
{
    /// <summary>
    /// A failed suite hook in the JSON results document.
    /// </summary>
    internal sealed class JsonLifecycleError
    {
        public string SuiteId { get; set; } = string.Empty;

        public string Stage { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
