using System.Text.Json;

namespace PlayOffsApi.Models;

public class BackgroundJobParameter
{
    public string Type { get; init; }
    public JsonElement Value { get; init; }
}