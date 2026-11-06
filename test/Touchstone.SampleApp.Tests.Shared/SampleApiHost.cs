namespace Touchstone.SampleApp.Tests.Shared
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Owns one isolated in-process Notes server and a client for it. Each case or scenario creates its own host and
    /// disposes it when it finishes, so building suites starts nothing and every host, including the one-test-per-case
    /// hosts that never run suite hooks, releases what it started.
    /// Configuration reload is disabled: watching the content root for changes is not needed in a test and can delay
    /// host start-up by seconds on some platforms.
    /// Thread safety: the client is safe for concurrent requests; dispose once.
    /// </summary>
    public sealed class SampleApiHost : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _Factory;
        private readonly HttpClient _Client;
        private bool _Disposed = false;

        /// <summary>
        /// Start an isolated server and create a client with an IPv4 loopback base address.
        /// </summary>
        public SampleApiHost()
        {
            _Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(AppContext.BaseDirectory);
                builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });
            _Client = _Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        }

        /// <summary>
        /// Client for the server. Never null.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The host was disposed.</exception>
        public HttpClient Client
        {
            get
            {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _Client;
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Client.Dispose();
            await _Factory.DisposeAsync().ConfigureAwait(false);
        }
    }
}
