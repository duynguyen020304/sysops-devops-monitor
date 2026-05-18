using System.Text.Json;

namespace Monitoring.Api.Services;

public static class AgentUpdateManifestTools
{
    public static string CanonicalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(ToCanonicalValue(doc.RootElement), new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(prop.Name); WriteCanonical(prop.Value, writer); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteCanonical(item, writer); writer.WriteEndArray(); break;
            case JsonValueKind.String: writer.WriteRawValue(JsonSerializer.Serialize(element.GetString()), skipInputValidation: true); break;
            case JsonValueKind.Number: writer.WriteRawValue(element.GetRawText()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            default: writer.WriteNullValue(); break;
        }
    }

    private static object? ToCanonicalValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(p => p.Name, p => ToCanonicalValue(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToCanonicalValue).ToArray(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDouble(out var d) ? d : element.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static string SignManifest(string canonicalManifestJson, string privateKeyPem)
    {
        using var key = System.Security.Cryptography.ECDsa.Create();
        key.ImportFromPem(privateKeyPem);
        var data = System.Text.Encoding.UTF8.GetBytes(canonicalManifestJson);
        var signature = key.SignData(data, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(signature);
    }
}
