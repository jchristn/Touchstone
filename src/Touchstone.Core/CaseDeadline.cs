namespace Touchstone.Core
{
    using System;
    using System.Threading;

    /// <summary>
    /// Timer for one case under <see cref="TestRunOptions.CaseTimeout"/>. <see cref="Token"/> is canceled by the caller's
    /// token or when the timeout elapses. <see cref="Stop"/> disarms the timer when the guarded work ends and records
    /// whether it had already fired, so work that runs afterwards (scenario cleanup, sink callbacks) cannot turn a
    /// case that finished in time into a timeout.
    /// Thread safety: all members are safe to call concurrently.
    /// </summary>
    internal sealed class CaseDeadline : IDisposable
    {
        private readonly CancellationTokenSource _Timer;
        private readonly CancellationTokenSource _Linked;
        private readonly object _Lock = new object();
        private readonly TimeSpan _Timeout;
        private bool _Stopped = false;
        private bool _Expired = false;
        private bool _Disposed = false;

        internal CaseDeadline(TimeSpan timeout, CancellationToken callerToken)
        {
            _Timeout = timeout;
            _Timer = new CancellationTokenSource(timeout);
            _Linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _Timer.Token);
        }

        internal TimeSpan Timeout
        {
            get { return _Timeout; }
        }

        internal CancellationToken Token
        {
            get { return _Linked.Token; }
        }

        internal bool Expired
        {
            get
            {
                Stop();
                lock (_Lock)
                {
                    return _Expired;
                }
            }
        }

        internal void Stop()
        {
            lock (_Lock)
            {
                if (_Stopped || _Disposed) return;
                _Stopped = true;
                _Timer.CancelAfter(System.Threading.Timeout.InfiniteTimeSpan);
                _Expired = _Timer.IsCancellationRequested;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            lock (_Lock)
            {
                if (_Disposed) return;
                if (!_Stopped)
                {
                    _Stopped = true;
                    _Expired = _Timer.IsCancellationRequested;
                }

                _Disposed = true;
            }

            if (disposing)
            {
                _Linked.Dispose();
                _Timer.Dispose();
            }
        }
    }
}
