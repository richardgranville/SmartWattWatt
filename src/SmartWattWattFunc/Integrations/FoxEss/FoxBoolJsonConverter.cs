using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartWattWattFunc.Integrations.FoxEss;

internal sealed class FoxBoolJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString() switch
            {
                "1" or "true" or "True" => true,
                "0" or "false" or "False" => false,
                _ => false
            },
            JsonTokenType.Number => reader.TryGetInt64(out var number) && number != 0,
            _ => throw new JsonException($"Unsupported JSON token for Fox ESS boolean: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}
