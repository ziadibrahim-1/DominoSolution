using System.Text.Json.Serialization;

namespace Domino.Shared.Models;

public class Player
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
