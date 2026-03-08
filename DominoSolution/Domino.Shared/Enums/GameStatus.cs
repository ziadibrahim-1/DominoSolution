using System.Text.Json.Serialization;

namespace Domino.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameStatus
{
    Waiting,
    Running,
    Ended
}
