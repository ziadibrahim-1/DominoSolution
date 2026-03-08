using System.Text.Json.Serialization;

namespace Domino.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoomStatus
{
    Waiting,
    Running,
    Full
}
