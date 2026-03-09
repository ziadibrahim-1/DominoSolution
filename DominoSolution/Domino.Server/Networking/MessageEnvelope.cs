using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domino.Server.Networking;

/// <summary>
/// Every message sent over TCP is wrapped in this envelope.
/// This way the server knows WHAT type of message it received
/// before deserializing the payload.
///
/// Wire format (one line on the TCP stream):
/// {"type":"LoginRequest","payload":{"playerName":"Abdallah"}}\n
/// </summary>
public record MessageEnvelope
{
    public string Type { get; init; } = string.Empty;
    public JsonElement Payload { get; init; } // now = the whole root element
}
