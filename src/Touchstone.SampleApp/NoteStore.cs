using System.Collections.Concurrent;

namespace Touchstone.SampleApp;

public sealed class NoteStore
{
    private readonly ConcurrentDictionary<string, Note> _notes = new();

    public Note Create(string title, string body)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var note = new Note(id, title, body);
        _notes[id] = note;
        return note;
    }

    public Note? Get(string id) => _notes.GetValueOrDefault(id);

    public bool Delete(string id) => _notes.TryRemove(id, out _);
}
