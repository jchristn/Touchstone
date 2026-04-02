namespace Touchstone.SampleApp.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Logging;

    using Touchstone.Core;

    /// <summary>
    /// Shared test suite descriptors for the sample Notes API.
    /// </summary>
    public static class SampleAppSuites
    {
        /// <summary>
        /// When set to true, includes an intentionally failing test case for verifying failure rendering.
        /// </summary>
        public static bool IncludeFailureToggle { get; set; } = false;

        /// <summary>
        /// All suites for the sample app.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { HealthSuite(), NotesSuite() }; }
        }

        /// <summary>
        /// Health endpoint suite.
        /// </summary>
        /// <returns>Suite descriptor for health checks.</returns>
        public static TestSuiteDescriptor HealthSuite()
        {
            WebApplicationFactory<Program> factory = CreateFactory();
            HttpClient client = factory.CreateClient();

            return new TestSuiteDescriptor(
                suiteId: "Health",
                displayName: "Health Endpoint",
                afterSuiteAsync: ct =>
                {
                    client.Dispose();
                    return factory.DisposeAsync();
                },
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Health",
                        caseId: "ReturnsOk",
                        displayName: "GET /health returns 200 OK",
                        executeAsync: async ct =>
                        {
                            HttpResponseMessage response = await client.GetAsync("/health", ct);
                            AssertEqual(HttpStatusCode.OK, response.StatusCode);
                        }),
                });
        }

        /// <summary>
        /// Notes CRUD suite.
        /// </summary>
        /// <returns>Suite descriptor for notes operations.</returns>
        public static TestSuiteDescriptor NotesSuite()
        {
            WebApplicationFactory<Program> factory = CreateFactory();
            HttpClient client = factory.CreateClient();

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "CreateReturnsId",
                    displayName: "POST /notes returns a note with an id",
                    executeAsync: async ct =>
                    {
                        HttpResponseMessage response = await client.PostAsJsonAsync(
                            "/notes",
                            new { Title = "Test", Body = "Content" },
                            ct);
                        AssertEqual(HttpStatusCode.Created, response.StatusCode);

                        NoteResponse note = await response.Content.ReadFromJsonAsync<NoteResponse>(ct);
                        if (note == null || string.IsNullOrEmpty(note.Id))
                            throw new InvalidOperationException("Note id should not be empty");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "FetchCreatedNote",
                    displayName: "GET /notes/{id} returns the stored payload",
                    executeAsync: async ct =>
                    {
                        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
                            "/notes",
                            new { Title = "Fetch Me", Body = "Payload" },
                            ct);
                        NoteResponse created = await DeserializeOrThrow(createResponse, ct);

                        HttpResponseMessage response = await client.GetAsync("/notes/" + created.Id, ct);
                        AssertEqual(HttpStatusCode.OK, response.StatusCode);

                        NoteResponse fetched = await DeserializeOrThrow(response, ct);
                        if (fetched.Title != "Fetch Me")
                            throw new InvalidOperationException(
                                "Expected title 'Fetch Me' but got '" + fetched.Title + "'");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "DeleteRemovesNote",
                    displayName: "DELETE /notes/{id} removes the note",
                    executeAsync: async ct =>
                    {
                        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
                            "/notes",
                            new { Title = "Delete Me", Body = "Gone" },
                            ct);
                        NoteResponse created = await DeserializeOrThrow(createResponse, ct);

                        HttpResponseMessage deleteResponse = await client.DeleteAsync("/notes/" + created.Id, ct);
                        AssertEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

                        HttpResponseMessage fetchResponse = await client.GetAsync("/notes/" + created.Id, ct);
                        AssertEqual(HttpStatusCode.NotFound, fetchResponse.StatusCode);
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "FetchMissingReturns404",
                    displayName: "GET /notes/{id} for missing note returns 404",
                    executeAsync: async ct =>
                    {
                        HttpResponseMessage response = await client.GetAsync("/notes/nonexistent", ct);
                        AssertEqual(HttpStatusCode.NotFound, response.StatusCode);
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "InvalidPayloadReturns400",
                    displayName: "POST /notes with empty body returns 400",
                    executeAsync: async ct =>
                    {
                        HttpResponseMessage response = await client.PostAsJsonAsync(
                            "/notes",
                            new { Title = "", Body = "" },
                            ct);
                        AssertEqual(HttpStatusCode.BadRequest, response.StatusCode);
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "SkippedExample",
                    displayName: "Placeholder for future pagination test",
                    skip: true,
                    skipReason: "Pagination not yet implemented",
                    executeAsync: _ => Task.CompletedTask),
            };

            if (IncludeFailureToggle)
            {
                cases.Add(new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "IntentionalFailure",
                    displayName: "Intentional failure for verifying failure rendering",
                    executeAsync: _ => throw new InvalidOperationException("This test intentionally fails.")));
            }

            return new TestSuiteDescriptor(
                suiteId: "Notes",
                displayName: "Notes CRUD",
                afterSuiteAsync: ct =>
                {
                    client.Dispose();
                    return factory.DisposeAsync();
                },
                cases: cases);
        }

        private static WebApplicationFactory<Program> CreateFactory()
        {
            return new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b =>
                {
                    b.UseContentRoot(AppContext.BaseDirectory);
                    b.ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.SetMinimumLevel(LogLevel.Warning);
                    });
                });
        }

        private static void AssertEqual(HttpStatusCode expected, HttpStatusCode actual)
        {
            if (expected != actual)
                throw new InvalidOperationException(
                    "Expected " + (int)expected + " " + expected + " but got " + (int)actual + " " + actual);
        }

        private static async Task<NoteResponse> DeserializeOrThrow(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            NoteResponse result = await response.Content.ReadFromJsonAsync<NoteResponse>(cancellationToken);
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize response");
            return result;
        }
    }
}
