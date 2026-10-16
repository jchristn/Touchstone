namespace Touchstone.Benchmark
{
    using System;
    using System.Collections.Generic;

    internal sealed class BenchmarkReport
    {
        /// <summary>Commit captured in the benchmark contract.</summary>
        public string Commit { get; set; } = string.Empty;
        /// <summary>SourceHash captured in the benchmark contract.</summary>
        public string SourceHash { get; set; } = string.Empty;
        /// <summary>DatasetHash captured in the benchmark contract.</summary>
        public string DatasetHash { get; set; } = string.Empty;
        /// <summary>ConfigurationHash captured in the benchmark contract.</summary>
        public string ConfigurationHash { get; set; } = string.Empty;
        /// <summary>DirtyState captured in the benchmark contract.</summary>
        public string DirtyState { get; set; } = string.Empty;
        /// <summary>Runtime captured in the benchmark contract.</summary>
        public string Runtime { get; set; } = string.Empty;
        /// <summary>Sdk captured in the benchmark contract.</summary>
        public string Sdk { get; set; } = string.Empty;
        /// <summary>Machine captured in the benchmark contract.</summary>
        public string Machine { get; set; } = string.Empty;
        /// <summary>OS captured in the benchmark contract.</summary>
        public string OS { get; set; } = string.Empty;
        /// <summary>CpuCount captured in the benchmark contract.</summary>
        public int CpuCount { get; set; }
        /// <summary>CpuModel captured in the benchmark contract.</summary>
        public string CpuModel { get; set; } = string.Empty;
        /// <summary>Count captured in the benchmark contract.</summary>
        public int Count { get; set; }
        /// <summary>Repetitions captured in the benchmark contract.</summary>
        public int Repetitions { get; set; }
        /// <summary>Warmups captured in the benchmark contract.</summary>
        public int Warmups { get; set; }
        /// <summary>CollectedAt captured in the benchmark contract.</summary>
        public DateTimeOffset CollectedAt { get; set; }
        /// <summary>Complete captured in the benchmark contract.</summary>
        public bool Complete { get; set; }
        /// <summary>Samples captured in the benchmark contract.</summary>
        public List<BenchmarkSample> Samples { get; set; } = new List<BenchmarkSample>();
    }
}
