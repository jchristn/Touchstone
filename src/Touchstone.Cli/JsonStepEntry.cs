#nullable enable
namespace Touchstone.Cli
{
    /// <summary>Typed JSON step result. Contains no exception objects or parameter capture.</summary>
    public sealed class JsonStepEntry
    {
        /// <summary>Stable step identity.</summary>
        public string StepId { get; set; } = string.Empty;
        /// <summary>Human-readable name.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Given, When, or Then.</summary>
        public string Kind { get; set; } = string.Empty;
        /// <summary>Passed, Failed, Skipped, Canceled, or NotRun.</summary>
        public string Outcome { get; set; } = string.Empty;
        /// <summary>Duration in milliseconds.</summary>
        public double DurationMs { get; set; }
        /// <summary>Optional failure or non-execution reason.</summary>
        public string? Message { get; set; }
    }
}
