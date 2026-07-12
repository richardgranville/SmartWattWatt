using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartWattWattFunc.Integrations.FoxEss;

internal sealed class FoxNullableBoolJsonConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString() switch
            {
                "1" or "true" or "True" => true,
                "0" or "false" or "False" => false,
                _ => null
            },
            JsonTokenType.Number => reader.TryGetInt64(out var number) ? number != 0 : null,
            _ => throw new JsonException($"Unsupported JSON token for Fox ESS boolean: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteBooleanValue(value.Value);
    }
}
