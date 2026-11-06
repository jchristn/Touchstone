namespace Touchstone.Cli
{
    /// <summary>
    /// One scenario step in the JSON results document.
    /// </summary>
    internal sealed class JsonStep
    {
        public string StepId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Outcome { get; set; } = string.Empty;

        public double DurationMs { get; set; }

        public string? Message { get; set; }
    }
}
