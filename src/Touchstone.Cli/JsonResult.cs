namespace Touchstone.Cli
{
    using System.Collections.Generic;

    /// <summary>
    /// One case result in the JSON results document.
    /// </summary>
    internal sealed class JsonResult
    {
        public string TestId { get; set; } = string.Empty;

        public string SuiteId { get; set; } = string.Empty;

        public string CaseId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Outcome { get; set; } = string.Empty;

        public double DurationMs { get; set; }

        public string? Message { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        public string? Feature { get; set; }

        public string? ExampleId { get; set; }

        public List<string> Requirements { get; set; } = new List<string>();

        public List<string> CleanupErrors { get; set; } = new List<string>();

        public List<JsonStep> Steps { get; set; } = new List<JsonStep>();
    }
}
