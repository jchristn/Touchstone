namespace Touchstone.SampleApp.Tests.Shared
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Logging;

    /// <summary>Owns an isolated in-process Notes server and client for one scenario execution.</summary>
    public sealed class NotesScenarioContext : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _Factory;
        private readonly HttpClient _Client;
        private string _NoteId = string.Empty;
        private HttpStatusCode _Status;
        private NoteResponse _Fetched;

        /// <summary>Create an isolated application and a client using an IPv4 loopback base URL.</summary>
        public NotesScenarioContext()
        {
            _Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(AppContext.BaseDirectory);
                builder.ConfigureLogging(logging => logging.ClearProviders());
            });
            _Client = _Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        }
        /// <summary>Create the note used by this scenario.</summary>
        /// <exception cref="InvalidOperationException">The server did not return a valid note.</exception>
        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await _Client.PostAsJsonAsync("/notes", new { Title = "Behavior", Body = "Stored content" }, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                NoteResponse note = await response.Content.ReadFromJsonAsync<NoteResponse>(cancellationToken).ConfigureAwait(false);
                if (note == null || string.IsNullOrEmpty(note.Id)) throw new InvalidOperationException("Creating a note must return its identity.");
                _NoteId = note.Id;
            }
        }
        /// <summary>Fetch the created note and check its observable content.</summary>
        /// <exception cref="InvalidOperationException">The note content differs from the created value.</exception>
        public async Task FetchAsync(CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await _Client.GetAsync("/notes/" + _NoteId, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                NoteResponse note = await response.Content.ReadFromJsonAsync<NoteResponse>(cancellationToken).ConfigureAwait(false);
                _Fetched = note;
            }
        }
        /// <summary>Check the previously fetched note's identity and content.</summary>
        /// <exception cref="InvalidOperationException">The fetched note differs from the created note.</exception>
        public Task VerifyContentAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_Fetched == null || _Fetched.Id != _NoteId || _Fetched.Body != "Stored content")
                throw new InvalidOperationException("Fetching the created note must preserve its identity and content.");
            return Task.CompletedTask;
        }
        /// <summary>Fetch a missing note.</summary>
        public async Task FetchMissingAsync(CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await _Client.GetAsync("/notes/missing", cancellationToken).ConfigureAwait(false)) _Status = response.StatusCode;
        }
        /// <summary>Submit an invalid payload.</summary>
        public async Task CreateInvalidAsync(CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await _Client.PostAsJsonAsync("/notes", new { Title = "", Body = "" }, cancellationToken).ConfigureAwait(false)) _Status = response.StatusCode;
        }
        /// <summary>Verify the last observed status.</summary>
        /// <exception cref="InvalidOperationException">Status differs from the expectation.</exception>
        public Task ExpectStatusAsync(HttpStatusCode expected, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_Status != expected) throw new InvalidOperationException("Expected " + expected + "; received " + _Status + ".");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _Client.Dispose();
            await _Factory.DisposeAsync().ConfigureAwait(false);
        }
    }
}
