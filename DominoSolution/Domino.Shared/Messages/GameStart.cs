using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record GameStart
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; init; } = string.Empty;
}
