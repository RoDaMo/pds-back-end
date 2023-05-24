using System.Text.Json;
using System.Text.Json.Serialization;
using PlayOffsApi.Models;

namespace PlayOffsApi.Middleware;

public class SportsAsNumberConverter : JsonConverter<Sports>
{
    // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
    public override Sports Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.Number when System.Enum.TryParse(typeToConvert, reader.GetInt32().ToString(), out var enumValue) => (Sports)enumValue,
            JsonTokenType.String when System.Enum.TryParse(typeToConvert, reader.GetString(), out var enumValue) => (Sports)enumValue,
            _ => throw new JsonException($"Unable to deserialize enum of type '{typeToConvert.FullName}'.")
        };
    
    public override void Write(Utf8JsonWriter writer, Sports value, JsonSerializerOptions options) => writer.WriteNumberValue((int)value);
}