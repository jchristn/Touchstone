namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// Validates JSON against the subset of JSON Schema used by the Touchstone results schema: type (single or list),
    /// const, enum, required, properties, additionalProperties false, items, and minimum.
    /// </summary>
    public static class SchemaValidator
    {
        /// <summary>
        /// Validate a document and return every violation, or an empty list when it is valid.
        /// </summary>
        /// <param name="schemaJson">Schema text.</param>
        /// <param name="documentJson">Document text.</param>
        /// <returns>Violations, each prefixed with a JSON path.</returns>
        public static IReadOnlyList<string> Validate(string schemaJson, string documentJson)
        {
            List<string> errors = new List<string>();
            using (JsonDocument schema = JsonDocument.Parse(schemaJson))
            using (JsonDocument document = JsonDocument.Parse(documentJson))
            {
                Check(schema.RootElement, document.RootElement, "$", errors);
            }

            return errors;
        }

        private static void Check(JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            if (schema.TryGetProperty("type", out JsonElement type))
            {
                IEnumerable<string> allowed = type.ValueKind == JsonValueKind.Array
                    ? type.EnumerateArray().Select(t => t.GetString()!)
                    : new[] { type.GetString()! };
                if (!allowed.Any(t => Matches(t, value)))
                {
                    errors.Add(path + ": expected " + string.Join("|", allowed) + " but found " + value.ValueKind);
                    return;
                }
            }

            if (schema.TryGetProperty("const", out JsonElement constant) && constant.GetRawText() != value.GetRawText())
                errors.Add(path + ": expected " + constant.GetRawText() + " but found " + value.GetRawText());

            if (schema.TryGetProperty("enum", out JsonElement options) && !options.EnumerateArray().Any(o => o.GetRawText() == value.GetRawText()))
                errors.Add(path + ": " + value.GetRawText() + " is not one of " + options.GetRawText());

            if (schema.TryGetProperty("minimum", out JsonElement minimum) && value.ValueKind == JsonValueKind.Number && value.GetDouble() < minimum.GetDouble())
                errors.Add(path + ": " + value.GetDouble() + " is below " + minimum.GetDouble());

            if (value.ValueKind == JsonValueKind.Object)
            {
                schema.TryGetProperty("properties", out JsonElement properties);
                if (schema.TryGetProperty("required", out JsonElement required))
                {
                    foreach (JsonElement name in required.EnumerateArray())
                    {
                        if (!value.TryGetProperty(name.GetString()!, out _)) errors.Add(path + ": missing required property " + name.GetString());
                    }
                }

                foreach (JsonProperty property in value.EnumerateObject())
                {
                    if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out JsonElement child))
                        Check(child, property.Value, path + "." + property.Name, errors);
                    else if (schema.TryGetProperty("additionalProperties", out JsonElement additional) && additional.ValueKind == JsonValueKind.False)
                        errors.Add(path + ": unexpected property " + property.Name);
                }
            }

            if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out JsonElement items))
            {
                int index = 0;
                foreach (JsonElement element in value.EnumerateArray())
                {
                    Check(items, element, path + "[" + index + "]", errors);
                    index++;
                }
            }
        }

        private static bool Matches(string type, JsonElement value)
        {
            switch (type)
            {
                case "object": return value.ValueKind == JsonValueKind.Object;
                case "array": return value.ValueKind == JsonValueKind.Array;
                case "string": return value.ValueKind == JsonValueKind.String;
                case "number": return value.ValueKind == JsonValueKind.Number;
                case "integer": return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _);
                case "boolean": return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False;
                case "null": return value.ValueKind == JsonValueKind.Null;
                default: throw new InvalidOperationException("Unsupported schema type " + type);
            }
        }
    }
}
