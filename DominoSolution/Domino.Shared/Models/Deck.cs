using System.Text.Json.Serialization;

namespace Domino.Shared.Models;

public static class Deck
{
    public static List<Card> GenerateStandardDoubleSix()
    {
        var tiles = new List<Card>();
        for (int left = 0; left <= 6; left++)
        {
            for (int right = left; right <= 6; right++)
            {
                tiles.Add(new Card(left, right));
            }
        }
        return tiles;
    }
}
