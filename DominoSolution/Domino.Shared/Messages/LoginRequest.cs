using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record LoginRequest
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; init; } = string.Empty;
}
