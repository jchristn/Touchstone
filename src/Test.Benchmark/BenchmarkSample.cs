namespace Touchstone.Benchmark
{
    internal sealed class BenchmarkSample
    {
        /// <summary>Arm captured in the benchmark contract.</summary>
        public string Arm { get; set; } = string.Empty;
        /// <summary>Listener captured in the benchmark contract.</summary>
        public bool Listener { get; set; }
        /// <summary>Cases captured in the benchmark contract.</summary>
        public int Cases { get; set; }
        /// <summary>Steps captured in the benchmark contract.</summary>
        public int Steps { get; set; }
        /// <summary>Failed captured in the benchmark contract.</summary>
        public bool Failed { get; set; }
        /// <summary>ElapsedMs captured in the benchmark contract.</summary>
        public double ElapsedMs { get; set; }
        /// <summary>AllocatedBytes captured in the benchmark contract.</summary>
        public long AllocatedBytes { get; set; }
        /// <summary>ProcessMs captured in the benchmark contract.</summary>
        public double ProcessMs { get; set; }
        /// <summary>PeakWorkingSetBytes captured in the benchmark contract.</summary>
        public long PeakWorkingSetBytes { get; set; }
        /// <summary>Repetition captured in the benchmark contract.</summary>
        public int Repetition { get; set; }
        /// <summary>ProbeId captured in the benchmark contract.</summary>
        public string ProbeId { get { return Arm + "/" + Listener + "/" + Repetition; } }
    }
}
