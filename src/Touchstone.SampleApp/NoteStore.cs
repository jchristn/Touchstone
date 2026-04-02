namespace Touchstone.SampleApp
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// In-memory store for notes.
    /// </summary>
    public sealed class NoteStore
    {
        private readonly ConcurrentDictionary<string, Note> _Notes = new ConcurrentDictionary<string, Note>();

        /// <summary>
        /// Create and store a new note.
        /// </summary>
        /// <param name="title">Note title.</param>
        /// <param name="body">Note body.</param>
        /// <returns>The created note with a generated identifier.</returns>
        public Note Create(string title, string body)
        {
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Note note = new Note(id, title, body);
            _Notes[id] = note;
            return note;
        }

        /// <summary>
        /// Retrieve a note by identifier.
        /// </summary>
        /// <param name="id">Note identifier.</param>
        /// <returns>The note, or null when not found.</returns>
        public Note Get(string id)
        {
            _Notes.TryGetValue(id, out Note note);
            return note;
        }

        /// <summary>
        /// Delete a note by identifier.
        /// </summary>
        /// <param name="id">Note identifier.</param>
        /// <returns>True when the note was removed.</returns>
        public bool Delete(string id)
        {
            return _Notes.TryRemove(id, out _);
        }
    }
}
