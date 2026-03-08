using System.Text.Json.Serialization;
using Domino.Shared.Models;

namespace Domino.Shared.Messages;

public record RoomsList
{
    [JsonPropertyName("rooms")]
    public List<Room> Rooms { get; init; } = new();
}
