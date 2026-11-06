namespace Touchstone.SampleApp.Tests.Shared
{
    /// <summary>
    /// Deserialization model for note responses from the sample app.
    /// </summary>
    public sealed class NoteResponse
    {
        /// <summary>
        /// Note identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Note title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Note body.
        /// </summary>
        public string Body { get; set; } = string.Empty;
    }
}
