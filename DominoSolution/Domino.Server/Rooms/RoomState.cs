using Domino.Shared.Enums;
using Domino.Shared.Models;
using Domino.Server.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Domino.Server.Rooms;

/// <summary>
/// Server-side mutable room state.
///
/// WHY a separate class from Ziad's Room model?
/// Ziad's Room uses "init" properties (immutable records) — perfect for sending over the network.
/// But on the server we need to ADD/REMOVE players, change status, etc.
/// So RoomState is the mutable working copy; Room is the snapshot we serialize and send.
/// </summary>
public class RoomState
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int ScoreLimit { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;

    // Players who are actively playing
    public List<PlayerSession> Players { get; } = new();

    // Watchers — receive GameState but cannot send moves
    public List<PlayerSession> Watchers { get; } = new();

    /// <summary>
    /// Convert to Ziad's immutable Room model for sending to clients.
    /// Clients only see names and IDs — not the PlayerSession objects.
    /// </summary>
    public Room ToRoomModel() => new Room
    {
        Id = Id,
        Name = Name,
        Capacity = Capacity,
        ScoreLimit = ScoreLimit,
        Status = Status,
        Players = Players
                        .Select(p => new Player { Id = p.PlayerId, Name = p.PlayerName })
                        .ToList()
    };
}
