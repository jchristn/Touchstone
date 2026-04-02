namespace Touchstone.SampleApp
{
    /// <summary>
    /// Request payload for creating a note.
    /// </summary>
    public sealed class CreateNoteRequest
    {
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
