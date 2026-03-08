using System.Text.Json.Serialization;
using Domino.Shared.Models;

namespace Domino.Shared.Messages;

public record GameState
{
    [JsonPropertyName("snapshot")]
    public GameSnapshot Snapshot { get; init; } = new GameSnapshot();
}
