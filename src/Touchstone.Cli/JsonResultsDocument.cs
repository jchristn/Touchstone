namespace Touchstone.Cli
{
    using System.Collections.Generic;

    /// <summary>
    /// Root of the JSON results document, schema 1.0.
    /// </summary>
    internal sealed class JsonResultsDocument
    {
        public string SchemaVersion { get; set; } = JsonReport.SchemaVersion;

        public JsonTool Tool { get; set; } = new JsonTool();

        public JsonRun Run { get; set; } = new JsonRun();

        public List<JsonResult> Results { get; set; } = new List<JsonResult>();
    }
}
