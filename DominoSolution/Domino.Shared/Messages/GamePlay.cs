using System.Text.Json.Serialization;
using Domino.Shared.Models;
using Domino.Shared.Enums;

namespace Domino.Shared.Messages;

public record GamePlay
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; init; } = string.Empty;

    [JsonPropertyName("card")]
    public Card Card { get; init; } = new Card(0,0);

    [JsonPropertyName("boardSide")]
    public BoardSide BoardSide { get; init; } = BoardSide.Left;
}
