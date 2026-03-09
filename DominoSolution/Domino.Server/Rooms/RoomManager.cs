using System.Collections.Concurrent;
using Domino.Shared.Enums;
using Domino.Shared.Messages;
using Domino.Shared.Models;
using Domino.Server.Networking;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Domino.Server.Rooms;

/// <summary>
/// Manages all rooms on the server.
///
/// Thread-safety:
/// ConcurrentDictionary = multiple threads can read/write rooms simultaneously without crashing.
/// _allSessions uses a plain lock{} because List<T> is not thread-safe.
/// </summary>
public class RoomManager
{
    private readonly ConcurrentDictionary<string, RoomState> _rooms = new();

    // All connected sessions (logged in or not) — used for broadcasting RoomsList to everyone
    private readonly List<PlayerSession> _allSessions = new();
    private readonly object _sessionsLock = new();

    // ──────────────────────────────────────────────
    // Session registration
    // ──────────────────────────────────────────────

    public void RegisterSession(PlayerSession session)
    {
        lock (_sessionsLock)
            _allSessions.Add(session);
    }

    public void UnregisterSession(PlayerSession session)
    {
        lock (_sessionsLock)
            _allSessions.Remove(session);

        // Clean up their room if they were in one
        if (!string.IsNullOrEmpty(session.CurrentRoomId))
            LeaveRoom(session);
    }

    // ──────────────────────────────────────────────
    // Room operations
    // ──────────────────────────────────────────────

    public (bool Success, string RoomId, string Error) CreateRoom(
        PlayerSession creator, string name, int capacity, int scoreLimit)
    {
        var room = new RoomState
        {
            Name = name,
            Capacity = capacity,
            ScoreLimit = scoreLimit
        };

        room.Players.Add(creator);
        creator.CurrentRoomId = room.Id;

        _rooms[room.Id] = room;

        BroadcastRoomsList(); // tell everyone a new room appeared
        return (true, room.Id, string.Empty);
    }

    public (bool Success, string Error) JoinRoom(
        PlayerSession session, string roomId, bool asWatcher = false)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return (false, "Room not found.");

        if (!asWatcher)
        {
            if (room.Status == RoomStatus.Running)
                return (false, "Game already started. Join as watcher instead.");

            if (room.Players.Count >= room.Capacity)
                return (false, "Room is full.");

            room.Players.Add(session);
            session.IsWatcher = false;

            // Mark full if capacity reached
            if (room.Players.Count >= room.Capacity)
                room.Status = RoomStatus.Full;
        }
        else
        {
            room.Watchers.Add(session);
            session.IsWatcher = true;
        }

        session.CurrentRoomId = roomId;
        BroadcastRoomsList();
        return (true, string.Empty);
    }

    public void LeaveRoom(PlayerSession session)
    {
        if (!_rooms.TryGetValue(session.CurrentRoomId, out var room)) return;

        room.Players.Remove(session);
        room.Watchers.Remove(session);
        session.CurrentRoomId = string.Empty;

        // If no players left, delete the room
        if (room.Players.Count == 0)
            _rooms.TryRemove(room.Id, out _);
        else
            room.Status = RoomStatus.Waiting; // re-open the room

        BroadcastRoomsList();
    }

    public void MarkRoomAsRunning(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.Status = RoomStatus.Running;
            BroadcastRoomsList();
        }
    }

    // ──────────────────────────────────────────────
    // Queries
    // ──────────────────────────────────────────────

    public RoomState? GetRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var room) ? room : null;

    public List<Room> GetAllRooms() =>
        _rooms.Values.Select(r => r.ToRoomModel()).ToList();

    // ──────────────────────────────────────────────
    // Broadcasting
    // ──────────────────────────────────────────────

    /// <summary>
    /// Send a message to all players AND watchers inside a specific room.
    /// </summary>
    public async Task BroadcastToRoomAsync<T>(string roomId, string type, T payload)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;

        var targets = room.Players.Concat(room.Watchers).ToList();
        await Task.WhenAll(targets.Select(s => s.SendAsync(type, payload)));
    }

    /// <summary>
    /// Send the updated rooms list to EVERY connected session.
    /// Called after any room change (create/join/leave/start).
    /// </summary>
    private void BroadcastRoomsList()
    {
        var message = new RoomsList { Rooms = GetAllRooms() };

        List<PlayerSession> sessions;
        lock (_sessionsLock)
            sessions = _allSessions.ToList(); // snapshot to avoid lock during async send

        foreach (var session in sessions)
            _ = session.SendAsync("RoomsList", message); // fire-and-forget per session
    }
}
