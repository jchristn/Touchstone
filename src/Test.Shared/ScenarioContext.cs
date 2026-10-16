namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;

    /// <summary>Owned mutable context used to verify lifecycle isolation.</summary>
    public sealed class ScenarioContext : IDisposable, IAsyncDisposable
    {
        /// <summary>Per-run value.</summary>
        public int Value { get; set; }
        /// <summary>Number of async disposal calls.</summary>
        public int AsyncDisposals { get; private set; }
        /// <summary>Number of sync disposal calls.</summary>
        public int SyncDisposals { get; private set; }
        /// <summary>Whether disposal should throw.</summary>
        public bool FailDisposal { get; set; }
        /// <inheritdoc />
        public void Dispose()
        {
            SyncDisposals++;
        }
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            if (FailDisposal) throw new InvalidOperationException("dispose failure");
            return ValueTask.CompletedTask;
        }
    }
}
