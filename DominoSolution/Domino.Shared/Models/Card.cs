using System.Text.Json.Serialization;

namespace Domino.Shared.Models;

public class Card
{
    [JsonPropertyName("left")]
    public int Left { get; init; }

    [JsonPropertyName("right")]
    public int Right { get; init; }

    [JsonIgnore]
    public bool IsDouble => Left == Right;

    public Card(int left, int right)
    {
        Left = left;
        Right = right;
    }
}
