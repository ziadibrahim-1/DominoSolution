using System.Text.Json.Serialization;

namespace Domino.Shared.Models;

public class Scoreboard
{
    [JsonPropertyName("playerTotals")]
    public Dictionary<string, int> PlayerTotals { get; init; } = new();
}
