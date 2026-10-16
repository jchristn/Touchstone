namespace Touchstone.Benchmark
{
    using System.Collections.Generic;

    internal sealed class BenchmarkComparison
    {
        /// <summary>Regression captured in the benchmark contract.</summary>
        public bool Regression { get; set; }
        /// <summary>Details captured in the benchmark contract.</summary>
        public List<string> Details { get; set; } = new List<string>();
        /// <summary>Tolerance captured in the benchmark contract.</summary>
        public double Tolerance { get; set; }
        /// <summary>BaselineCommit captured in the benchmark contract.</summary>
        public string BaselineCommit { get; set; } = string.Empty;
        /// <summary>CandidateCommit captured in the benchmark contract.</summary>
        public string CandidateCommit { get; set; } = string.Empty;
    }
}
