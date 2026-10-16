namespace Touchstone.SampleApp.Tests.Shared
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>Agreed observable Notes behaviors, shared unchanged across every host.</summary>
    public static class NotesScenarios
    {
        /// <summary>Build fresh scenario definitions. Servers are created during execution, never discovery.</summary>
        public static TestSuiteDescriptor Suite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Builder("CreatePersists", "Creating a note preserves its content")
                    .Given("create", "a note has been created", (context, ct) => context.CreateAsync(ct))
                    .When("fetch", "the note is fetched", (context, ct) => context.FetchAsync(ct))
                    .Then("verified", "its identity and content match", (context, ct) => context.VerifyContentAsync(ct)).Build(),
                Builder("MissingNote", "Fetching a missing note reports not found")
                    .When("fetch", "a missing note is fetched", (context, ct) => context.FetchMissingAsync(ct))
                    .Then("notFound", "the result is not found", (context, ct) => context.ExpectStatusAsync(HttpStatusCode.NotFound, ct)).Build(),
                Builder("InvalidNote", "Creating an empty note is rejected")
                    .When("create", "an empty note is submitted", (context, ct) => context.CreateInvalidAsync(ct))
                    .Then("badRequest", "the result is a bad request", (context, ct) => context.ExpectStatusAsync(HttpStatusCode.BadRequest, ct)).Build()
            };
            if (SampleAppSuites.IncludeFailureToggle)
                cases.Add(Builder("IntentionalScenarioFailure", "Intentional scenario failure")
                    .When("fetch", "a missing note is fetched", (context, ct) => context.FetchMissingAsync(ct))
                    .Then("incorrect", "the deliberately incorrect expectation fails", (context, ct) => context.ExpectStatusAsync(HttpStatusCode.OK, ct)).Build());
            return new TestSuiteDescriptor("NotesBehavior", "Notes behaviors", cases);
        }
        private static ScenarioBuilder<NotesScenarioContext> Builder(string caseId, string name)
        {
            return new ScenarioBuilder<NotesScenarioContext>("NotesBehavior", caseId, name, ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new NotesScenarioContext());
            }).WithMetadata(new ScenarioMetadata("Notes persistence and validation", new[] { "NOTES-BEHAVIOR" }));
        }
    }
}
