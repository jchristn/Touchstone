using Touchstone.SampleApp;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<NoteStore>();

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok("healthy"));

app.MapPost("/notes", (CreateNoteRequest request, NoteStore store) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        return Results.BadRequest(new { error = "Title and Body are required." });

    Note note = store.Create(request.Title, request.Body);
    return Results.Created("/notes/" + note.Id, note);
});

app.MapGet("/notes/{id}", (string id, NoteStore store) =>
{
    Note note = store.Get(id);
    return note != null ? Results.Ok(note) : Results.NotFound();
});

app.MapDelete("/notes/{id}", (string id, NoteStore store) =>
{
    return store.Delete(id) ? Results.NoContent() : Results.NotFound();
});

app.Run();

/// <summary>
/// Partial program class for WebApplicationFactory entry point discovery.
/// </summary>
public partial class Program { }
