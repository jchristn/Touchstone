namespace Touchstone.SampleApp
{
    /// <summary>
    /// A stored note.
    /// </summary>
    public sealed class Note
    {
        /// <summary>
        /// Initialize a new note.
        /// </summary>
        /// <param name="id">Unique identifier.</param>
        /// <param name="title">Note title.</param>
        /// <param name="body">Note body.</param>
        public Note(string id, string title, string body)
        {
            Id = id;
            Title = title;
            Body = body;
        }

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Note title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Note body.
        /// </summary>
        public string Body { get; set; }
    }
}
