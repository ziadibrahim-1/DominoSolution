using System.Text.Json.Serialization;

namespace Domino.Shared.Messages;

public record LoginResponse
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; init; } = string.Empty;
}
