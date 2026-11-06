namespace Touchstone.Cli
{
    using System.Collections.Generic;

    /// <summary>
    /// Run-level data in the JSON results document.
    /// </summary>
    internal sealed class JsonRun
    {
        public string StartedUtc { get; set; } = string.Empty;

        public double DurationMs { get; set; }

        public bool Canceled { get; set; }

        public JsonSummary Summary { get; set; } = new JsonSummary();

        public List<JsonLifecycleError> LifecycleErrors { get; set; } = new List<JsonLifecycleError>();
    }
}
