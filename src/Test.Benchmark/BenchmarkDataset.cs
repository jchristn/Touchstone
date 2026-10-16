namespace Touchstone.Benchmark
{
    using System.Collections.Generic;

    internal sealed class BenchmarkDataset
    {
        /// <summary>Dataset schema version.</summary>
        public int Schema { get; set; }
        /// <summary>Deterministic seed.</summary>
        public int Seed { get; set; }
        /// <summary>Cases in each non-floor arm.</summary>
        public int Cases { get; set; }
        /// <summary>Workload arm names.</summary>
        public string[] Arms { get; set; } = System.Array.Empty<string>();
        /// <summary>Expected steps per case by arm.</summary>
        public Dictionary<string, int> Steps { get; set; } = new Dictionary<string, int>();
        /// <summary>Listener modes to measure.</summary>
        public bool[] Listeners { get; set; } = System.Array.Empty<bool>();
        /// <summary>Human-readable correctness oracle.</summary>
        public string Oracle { get; set; } = string.Empty;
    }
}
