using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Vecerdi.Extensions.Configuration;

/// <summary>
/// Flattens a JSON document into configuration keys: objects nest with <c>:</c>, arrays index from
/// <c>0</c>, scalars become strings, <c>null</c> stays <c>null</c>. Comments and trailing commas are
/// allowed. Keys are compared case-insensitively, as everywhere in Microsoft.Extensions.Configuration.
/// </summary>
public static class JsonConfigurationParser {
    private static readonly JsonDocumentOptions s_Options = new() {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static Dictionary<string, string?> Parse(string json) {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        ParseInto(json, data);
        return data;
    }

    /// <summary>Parses <paramref name="json"/> on top of <paramref name="data"/>; later keys overwrite earlier ones.</summary>
    public static void ParseInto(string json, IDictionary<string, string?> data) {
        using var document = JsonDocument.Parse(json, s_Options);
        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            throw new FormatException($"Top-level JSON element must be an object, not {document.RootElement.ValueKind}.");
        }

        Visit(document.RootElement, null, data);
    }

    private static void Visit(JsonElement element, string? path, IDictionary<string, string?> data) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) {
                    Visit(property.Value, path is null ? property.Name : ConfigurationPath.Combine(path, property.Name), data);
                }

                break;
            case JsonValueKind.Array: {
                var index = 0;
                foreach (var item in element.EnumerateArray()) {
                    Visit(item, ConfigurationPath.Combine(path!, index.ToString()), data);
                    index++;
                }

                break;
            }
            case JsonValueKind.Null:
                data[path!] = null;
                break;
            default:
                data[path!] = element.ToString();
                break;
        }
    }
}
