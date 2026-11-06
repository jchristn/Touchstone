namespace Touchstone.Cli
{
    /// <summary>
    /// Tool name and version in the JSON results document.
    /// </summary>
    internal sealed class JsonTool
    {
        public string Name { get; set; } = "Touchstone";

        public string Version { get; set; } = string.Empty;
    }
}
