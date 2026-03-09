using Domino.Server.Networking;
using Domino.Server.Rooms;
using Domino.Shared.Enums;
using Domino.Shared.Messages;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Domino.Server.Handlers;

/// <summary>
/// Reads a MessageEnvelope, figures out the type, deserializes the payload,
/// and calls the correct handler method.
///
/// Think of this as a traffic controller:
/// raw JSON line → MessageEnvelope → correct C# record → handler
/// </summary>
public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public MessageDispatcher(RoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public async Task DispatchAsync(PlayerSession session, MessageEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case "LoginRequest":
                await HandleLoginAsync(session, Deserialize<LoginRequest>(envelope));
                break;

            case "RoomCreateRequest":
                await HandleCreateRoomAsync(session, Deserialize<RoomCreateRequest>(envelope));
                break;

            case "RoomJoinRequest":
                await HandleJoinRoomAsync(session, Deserialize<RoomJoinRequest>(envelope));
                break;

            case "RoomLeaveRequest":
                await HandleLeaveRoomAsync(session, Deserialize<RoomLeaveRequest>(envelope));
                break;

            case "GameStart":
                await HandleGameStartAsync(session, Deserialize<GameStart>(envelope));
                break;

            case "GamePlay":
                await HandleGamePlayAsync(session, Deserialize<GamePlay>(envelope));
                break;

            case "GamePass":
                await HandleGamePassAsync(session, Deserialize<GamePass>(envelope));
                break;

            default:
                Console.WriteLine($"[Dispatcher] Unknown message type: '{envelope.Type}'");
                break;
        }
    }

    // ──────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────

    private async Task HandleLoginAsync(PlayerSession session, LoginRequest msg)
    {
        session.PlayerId = Guid.NewGuid().ToString();
        session.PlayerName = msg.PlayerName;

        Console.WriteLine($"[Login] '{msg.PlayerName}' connected. ID={session.PlayerId}");

        // 1. Confirm login with their assigned ID
        await session.SendAsync("LoginResponse", new LoginResponse
        {
            PlayerId = session.PlayerId
        });

        // 2. Send the current rooms list so their UI can show it immediately
        await session.SendAsync("RoomsList", new RoomsList
        {
            Rooms = _roomManager.GetAllRooms()
        });
    }

    private async Task HandleCreateRoomAsync(PlayerSession session, RoomCreateRequest msg)
    {
        var (success, roomId, error) = _roomManager.CreateRoom(
            session, msg.Name, msg.Capacity, msg.ScoreLimit);

        Console.WriteLine(success
            ? $"[Room] '{session.PlayerName}' created room '{msg.Name}' (id={roomId})"
            : $"[Room] Create failed: {error}");
    }

    private async Task HandleJoinRoomAsync(PlayerSession session, RoomJoinRequest msg)
    {
        // If the game is already running, auto-assign as watcher
        var room = _roomManager.GetRoom(msg.RoomId);
        bool asWatcher = room?.Status == RoomStatus.Running;

        var (success, error) = _roomManager.JoinRoom(session, msg.RoomId, asWatcher);

        if (!success)
        {
            Console.WriteLine($"[Room] '{session.PlayerName}' failed to join {msg.RoomId}: {error}");
            return;
        }

        Console.WriteLine($"[Room] '{session.PlayerName}' joined room {msg.RoomId}" +
                          (asWatcher ? " as WATCHER" : ""));
    }

    private async Task HandleLeaveRoomAsync(PlayerSession session, RoomLeaveRequest msg)
    {
        _roomManager.LeaveRoom(session);
        Console.WriteLine($"[Room] '{session.PlayerName}' left room {msg.RoomId}");
    }

    private async Task HandleGameStartAsync(PlayerSession session, GameStart msg)
    {
        _roomManager.MarkRoomAsRunning(msg.RoomId);

        Console.WriteLine($"[Game] Start requested for room {msg.RoomId}");

        // TODO: call Bishoy's DeckService.Deal() here to get initial hands
        // TODO: build GameSnapshot with SnapshotBuilder and broadcast GameState

        await _roomManager.BroadcastToRoomAsync(msg.RoomId, "GameStart", msg);
    }

    private async Task HandleGamePlayAsync(PlayerSession session, GamePlay msg)
    {
        Console.WriteLine($"[Game] '{session.PlayerName}' played " +
                          $"{msg.Card.Left}|{msg.Card.Right} on {msg.BoardSide} in room {msg.RoomId}");

        // TODO: call Bishoy's RulesService.IsLegalMove() — reject if illegal
        // TODO: call RulesService.ApplyMove() to update board state
        // TODO: call TurnService.Advance() to move to next player
        // TODO: build new GameSnapshot and broadcast GameState to room

        await _roomManager.BroadcastToRoomAsync(msg.RoomId, "GamePlay", msg);
    }

    private async Task HandleGamePassAsync(PlayerSession session, GamePass msg)
    {
        Console.WriteLine($"[Game] '{session.PlayerName}' passed in room {msg.RoomId}");

        // TODO: call Bishoy's CanPass() — reject if boneyard is not empty
        // TODO: call TurnService.Advance()
        // TODO: broadcast updated GameState

        await _roomManager.BroadcastToRoomAsync(msg.RoomId, "GamePass", msg);
    }

    // ──────────────────────────────────────────────
    // Helper
    // ──────────────────────────────────────────────

    /// <summary>
    /// Deserialize the raw JsonElement payload into the correct type T.
    /// GetRawText() returns the JSON string inside the element so we can deserialize it.
    /// </summary>
    private T Deserialize<T>(MessageEnvelope envelope) =>
    JsonSerializer.Deserialize<T>(envelope.Payload.GetRawText(), _jsonOptions)!;
}
