using System.Text.Json;
using System.Text.Json.Serialization;
using PlayOffsApi.Enum;

namespace PlayOffsApi.Middleware;

public class FormatAsNumberConverter : JsonConverter<Format>
{
    // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
    public override Format Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.Number when System.Enum.TryParse(typeToConvert, reader.GetInt32().ToString(), out var enumValue) => (Format)enumValue,
            JsonTokenType.String when System.Enum.TryParse(typeToConvert, reader.GetString(), out var enumValue) => (Format)enumValue,
            _ => throw new JsonException($"Unable to deserialize enum of type '{typeToConvert.FullName}'.")
        };
    
    public override void Write(Utf8JsonWriter writer, Format value, JsonSerializerOptions options) => writer.WriteNumberValue((int)value);
}