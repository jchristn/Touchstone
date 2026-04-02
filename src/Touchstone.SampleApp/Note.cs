namespace Touchstone.SampleApp;

public sealed record Note(string Id, string Title, string Body);

public sealed record CreateNoteRequest(string? Title, string? Body);
