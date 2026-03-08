using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record RoomLeaveRequest
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; init; } = string.Empty;
}
