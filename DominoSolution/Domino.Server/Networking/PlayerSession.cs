using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Server.Networking;

/// <summary>
/// Represents one connected client.
///
/// Memory/Threading note:
/// Each PlayerSession holds a StreamReader + StreamWriter over the TcpClient's NetworkStream.
/// NetworkStream lives on the HEAP — it wraps the OS socket handle.
/// SemaphoreSlim prevents two threads writing to the stream at the same time
/// (e.g. two broadcasts arriving simultaneously would corrupt the stream without it).
/// </summary>
public class PlayerSession
{
    // --- Identity ---
    public string ConnectionId { get; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public bool IsWatcher { get; set; } = false;
    public string CurrentRoomId { get; set; } = string.Empty;

    // --- Stream I/O ---
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;

    // SemaphoreSlim(1,1) = a lock that works with async/await.
    // Regular lock{} blocks the thread; SemaphoreSlim releases the thread while waiting.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public PlayerSession(TcpClient client)
    {
        var stream = client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    /// <summary>
    /// Serialize the payload into a MessageEnvelope and send it as one JSON line.
    /// Thread-safe: uses SemaphoreSlim so multiple broadcasts don't collide.
    /// </summary>
    public async Task SendAsync<T>(string type, T payload)
    {
        // Merge "type" field directly into the payload object
        // instead of wrapping it in a "payload" property
        var dict = JsonSerializer.SerializeToElement(payload, _jsonOptions)
                                 .Deserialize<Dictionary<string, JsonElement>>(_jsonOptions)
                   ?? new();

        dict["type"] = JsonSerializer.SerializeToElement(type);

        var json = JsonSerializer.Serialize(dict, _jsonOptions);

        await _writeLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Read one line from the stream = one complete JSON message.
    /// Returns null when the client disconnects.
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        return await _reader.ReadLineAsync(ct);
    }
}
