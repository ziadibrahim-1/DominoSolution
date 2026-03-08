using System.Text.Json.Serialization;
using Domino.Shared.Enums;

namespace Domino.Shared.Models;

public class Room
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("players")]
    public List<Player> Players { get; init; } = new();

    [JsonPropertyName("status")]
    public RoomStatus Status { get; init; } = RoomStatus.Waiting;

    [JsonPropertyName("scoreLimit")]
    public int ScoreLimit { get; init; }
}
