using System.Text.Json.Serialization;
using Domino.Shared.Enums;

namespace Domino.Shared.Models;

public class GameSnapshot
{
    [JsonPropertyName("boardLeft")]
    public int? BoardLeft { get; init; }

    [JsonPropertyName("boardRight")]
    public int? BoardRight { get; init; }

    [JsonPropertyName("currentPlayerId")]
    public string CurrentPlayerId { get; init; } = string.Empty;

    [JsonPropertyName("handsByPlayerId")]
    public Dictionary<string, List<Card>> HandsByPlayerId { get; init; } = new();

    [JsonPropertyName("boneyardCount")]
    public int BoneyardCount { get; init; }

    [JsonPropertyName("gameStatus")]
    public GameStatus GameStatus { get; init; } = GameStatus.Waiting;
}
