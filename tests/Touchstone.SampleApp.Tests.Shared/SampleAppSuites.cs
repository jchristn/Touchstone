using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Touchstone.Core;
using Touchstone.SampleApp;

namespace Touchstone.SampleApp.Tests.Shared;

public static class SampleAppSuites
{
    public static IReadOnlyList<TestSuiteDescriptor> All => [HealthSuite(), NotesSuite()];

    public static TestSuiteDescriptor HealthSuite()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseContentRoot(AppContext.BaseDirectory);
                b.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });
        var client = factory.CreateClient();

        return new TestSuiteDescriptor(
            suiteId: "Health",
            displayName: "Health Endpoint",
            afterSuiteAsync: ct =>
            {
                client.Dispose();
                return factory.DisposeAsync();
            },
            cases:
            [
                new TestCaseDescriptor(
                    suiteId: "Health",
                    caseId: "ReturnsOk",
                    displayName: "GET /health returns 200 OK",
                    executeAsync: async ct =>
                    {
                        var response = await client.GetAsync("/health", ct);
                        Assert(response.StatusCode == HttpStatusCode.OK,
                            $"Expected 200 OK but got {(int)response.StatusCode}");
                    }),
            ]);
    }

    public static TestSuiteDescriptor NotesSuite()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseContentRoot(AppContext.BaseDirectory);
                b.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });
        var client = factory.CreateClient();

        return new TestSuiteDescriptor(
            suiteId: "Notes",
            displayName: "Notes CRUD",
            afterSuiteAsync: ct =>
            {
                client.Dispose();
                return factory.DisposeAsync();
            },
            cases:
            [
                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "CreateReturnsId",
                    displayName: "POST /notes returns a note with an id",
                    executeAsync: async ct =>
                    {
                        var response = await client.PostAsJsonAsync("/notes",
                            new { Title = "Test", Body = "Content" }, ct);
                        Assert(response.StatusCode == HttpStatusCode.Created,
                            $"Expected 201 Created but got {(int)response.StatusCode}");

                        var note = await response.Content.ReadFromJsonAsync<NoteResponse>(ct);
                        Assert(!string.IsNullOrEmpty(note?.Id), "Note id should not be empty");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "FetchCreatedNote",
                    displayName: "GET /notes/{id} returns the stored payload",
                    executeAsync: async ct =>
                    {
                        var createResponse = await client.PostAsJsonAsync("/notes",
                            new { Title = "Fetch Me", Body = "Payload" }, ct);
                        var created = await createResponse.Content.ReadFromJsonAsync<NoteResponse>(ct)
                            ?? throw new InvalidOperationException("Failed to deserialize created note");

                        var response = await client.GetAsync($"/notes/{created.Id}", ct);
                        Assert(response.StatusCode == HttpStatusCode.OK,
                            $"Expected 200 OK but got {(int)response.StatusCode}");

                        var fetched = await response.Content.ReadFromJsonAsync<NoteResponse>(ct);
                        Assert(fetched?.Title == "Fetch Me",
                            $"Expected title 'Fetch Me' but got '{fetched?.Title}'");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "DeleteRemovesNote",
                    displayName: "DELETE /notes/{id} removes the note",
                    executeAsync: async ct =>
                    {
                        var createResponse = await client.PostAsJsonAsync("/notes",
                            new { Title = "Delete Me", Body = "Gone" }, ct);
                        var created = await createResponse.Content.ReadFromJsonAsync<NoteResponse>(ct)
                            ?? throw new InvalidOperationException("Failed to deserialize created note");

                        var deleteResponse = await client.DeleteAsync($"/notes/{created.Id}", ct);
                        Assert(deleteResponse.StatusCode == HttpStatusCode.NoContent,
                            $"Expected 204 NoContent but got {(int)deleteResponse.StatusCode}");

                        var fetchResponse = await client.GetAsync($"/notes/{created.Id}", ct);
                        Assert(fetchResponse.StatusCode == HttpStatusCode.NotFound,
                            $"Expected 404 after delete but got {(int)fetchResponse.StatusCode}");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "FetchMissingReturns404",
                    displayName: "GET /notes/{id} for missing note returns 404",
                    executeAsync: async ct =>
                    {
                        var response = await client.GetAsync("/notes/nonexistent", ct);
                        Assert(response.StatusCode == HttpStatusCode.NotFound,
                            $"Expected 404 NotFound but got {(int)response.StatusCode}");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "InvalidPayloadReturns400",
                    displayName: "POST /notes with empty body returns 400",
                    executeAsync: async ct =>
                    {
                        var response = await client.PostAsJsonAsync("/notes",
                            new { Title = "", Body = "" }, ct);
                        Assert(response.StatusCode == HttpStatusCode.BadRequest,
                            $"Expected 400 BadRequest but got {(int)response.StatusCode}");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Notes",
                    caseId: "SkippedExample",
                    displayName: "Placeholder for future pagination test",
                    skip: true,
                    skipReason: "Pagination not yet implemented",
                    executeAsync: _ => Task.CompletedTask),
            ]);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed record NoteResponse(string Id, string Title, string Body);
}
