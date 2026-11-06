namespace Touchstone.SampleApp
{
    /// <summary>
    /// Request payload for creating a note.
    /// </summary>
    public sealed class CreateNoteRequest
    {
        /// <summary>
        /// Note title. Null when the client omits it.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Note body. Null when the client omits it.
        /// </summary>
        public string? Body { get; set; }
    }
}
