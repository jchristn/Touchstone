using Touchstone.SampleApp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<NoteStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("healthy"));

app.MapPost("/notes", (CreateNoteRequest? request, NoteStore store) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        return Results.BadRequest(new { error = "Title and Body are required." });

    var note = store.Create(request.Title, request.Body);
    return Results.Created($"/notes/{note.Id}", note);
});

app.MapGet("/notes/{id}", (string id, NoteStore store) =>
{
    var note = store.Get(id);
    return note is not null ? Results.Ok(note) : Results.NotFound();
});

app.MapDelete("/notes/{id}", (string id, NoteStore store) =>
{
    return store.Delete(id) ? Results.NoContent() : Results.NotFound();
});

app.Run();

public partial class Program;
