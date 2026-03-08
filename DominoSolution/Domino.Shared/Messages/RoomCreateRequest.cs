using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record RoomCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("scoreLimit")]
    public int ScoreLimit { get; init; }
}
