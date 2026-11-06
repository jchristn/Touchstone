namespace Touchstone.Cli
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated serialization for the JSON results document, so export works under trimming and native AOT.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
    [JsonSerializable(typeof(JsonResultsDocument))]
    internal sealed partial class TouchstoneJsonContext : JsonSerializerContext
    {
    }
}
