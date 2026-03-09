using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Domino.Server.Handlers;
using Domino.Server.Rooms;

namespace Domino.Server.Networking;

/// <summary>
/// The entry point of all networking.
///
/// What happens under the hood:
/// 1. TcpListener binds to a port and tells the OS "I want to accept connections here"
/// 2. AcceptTcpClientAsync() is an async wait — the thread is FREE while waiting
///    (it's not blocked, it yields to the thread pool)
/// 3. When a client connects, the OS wakes us up and gives us a TcpClient
/// 4. We create a PlayerSession for that client and start HandleClientAsync in the background
/// 5. The main loop goes back to waiting for the NEXT client
///
/// This means 100 clients = 100 tasks running concurrently, not 100 blocked threads.
/// </summary>
public class TcpServer
{
    private readonly int _port;
    private readonly RoomManager _roomManager;
    private readonly MessageDispatcher _dispatcher;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public TcpServer(int port)
    {
        _port = port;
        _roomManager = new RoomManager();
        _dispatcher = new MessageDispatcher(_roomManager);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[Server] Started. Listening on port {_port}...");

        while (!ct.IsCancellationRequested)
        {
            // Await the next client — thread is free during this wait
            var client = await listener.AcceptTcpClientAsync(ct);
            Console.WriteLine($"[Server] Client connected from {client.Client.RemoteEndPoint}");

            // Start handling this client in the background (don't await — keep accepting new clients)
            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var session = new PlayerSession(client);
        _roomManager.RegisterSession(session);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // ReadLineAsync blocks (asynchronously) until \n is received = one full message
                var line = await session.ReadLineAsync(ct);

                if (line is null) break; // null = client disconnected cleanly

                // Parse the envelope to know the message type
                using var doc = JsonDocument.Parse(line);
                var type = doc.RootElement.GetProperty("type").GetString() ?? "";
                var envelope = new MessageEnvelope
                {
                    Type = type,
                    Payload = doc.RootElement
                };
                if (envelope is null) continue;

                // Route to the correct handler
                await _dispatcher.DispatchAsync(session, envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // Server is shutting down — normal, not an error
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Error for '{session.PlayerName}': {ex.Message}");
        }
        finally
        {
            // Always clean up, even if an exception occurred
            _roomManager.UnregisterSession(session);
            client.Close();
            Console.WriteLine($"[Server] Session ended for '{session.PlayerName}'");
        }
    }
}
