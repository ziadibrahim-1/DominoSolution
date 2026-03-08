using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record GamePass
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; init; } = string.Empty;
}
