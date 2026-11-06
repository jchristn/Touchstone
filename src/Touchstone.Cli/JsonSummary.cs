namespace Touchstone.Cli
{
    /// <summary>
    /// Outcome counts in the JSON results document.
    /// </summary>
    internal sealed class JsonSummary
    {
        public int Total { get; set; }

        public int Passed { get; set; }

        public int Failed { get; set; }

        public int Skipped { get; set; }

        public int Canceled { get; set; }

        public int NotRun { get; set; }

        public int Filtered { get; set; }
    }
}
