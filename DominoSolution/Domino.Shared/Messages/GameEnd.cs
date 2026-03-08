using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record GameEnd
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; init; } = string.Empty;

    [JsonPropertyName("finalScores")]
    public Dictionary<string, int> FinalScores { get; init; } = new();
}
