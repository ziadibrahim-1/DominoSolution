using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Shared.Messages;
using Domino.Shared.Models;
using Domino.Shared.Enums;

namespace Domino.Client.Win.Network;

/// <summary>
/// High-level façade used directly by WinForms Forms.
/// Combines <see cref="TcpNetworkClient"/> + <see cref="MessageDispatcher"/>
/// and exposes one method per UI action, using the exact
/// <c>Domino.Shared.Messages</c> record types from the shared project.
///
/// <code>
/// // In your Form constructor:
/// _net = new NetworkService();
/// _net.Dispatcher.OnLoginResponse += (_, r)  => HandleLogin(r);
/// _net.Dispatcher.OnRoomsList     += (_, r)  => RefreshRooms(r);
/// _net.Dispatcher.OnGameStart     += (_, gs) => OpenGameForm(gs);
/// _net.Dispatcher.OnGameState     += (_, gs) => UpdateBoard(gs);
/// _net.Dispatcher.OnGameEnd       += (_, ge) => ShowResults(ge);
/// await _net.ConnectAsync();
/// </code>
/// </summary>
public sealed class NetworkService : IAsyncDisposable
{
    private readonly TcpNetworkClient _client;

    public MessageDispatcher Dispatcher { get; }

    /// <summary>Forward connection state changes from the underlying client.</summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? OnConnectionStateChanged
    {
        add    => _client.OnConnectionStateChanged += value;
        remove => _client.OnConnectionStateChanged -= value;
    }

    public bool IsConnected => _client.IsConnected;

    /// <param name="host">Server hostname or IP. Defaults to localhost.</param>
    /// <param name="port">Server port. Defaults to 5000 per the protocol spec.</param>
    public NetworkService()
    {
        _client    = new TcpNetworkClient();
        Dispatcher = new MessageDispatcher(_client);
    }

    // ── Transport ─────────────────────────────────────────────────────────────
    public Task ConnectAsync(string host = "localhost", int port = 5000,
                             CancellationToken ct = default)
        => _client.ConnectAsync(host, port, ct);

    public Task DisconnectAsync() => _client.DisconnectAsync();

    // ── UI → Network actions ──────────────────────────────────────────────────

    /// <summary>
    /// Login → <see cref="LoginRequest"/>
    /// Sends the player's chosen display name. No password in this protocol.
    /// </summary>
    public Task LoginAsync(string playerName, CancellationToken ct = default) =>
        _client.SendAsync(new LoginRequest { PlayerName = playerName }, ct);

    /// <summary>
    /// Create Room → <see cref="RoomCreateRequest"/>
    /// </summary>
    public Task CreateRoomAsync(string name, int capacity, int scoreLimit,
                                CancellationToken ct = default) =>
        _client.SendAsync(new RoomCreateRequest
        {
            Name       = name,
            Capacity   = capacity,
            ScoreLimit = scoreLimit
        }, ct);

    /// <summary>
    /// Join Room → <see cref="RoomJoinRequest"/>
    /// </summary>
    public Task JoinRoomAsync(string roomId, CancellationToken ct = default) =>
        _client.SendAsync(new RoomJoinRequest { RoomId = roomId }, ct);

    /// <summary>
    /// Leave Room → <see cref="RoomLeaveRequest"/>
    /// </summary>
    public Task LeaveRoomAsync(string roomId, CancellationToken ct = default) =>
        _client.SendAsync(new RoomLeaveRequest { RoomId = roomId }, ct);

    /// <summary>
    /// Play Card → <see cref="GamePlay"/>
    /// </summary>
    public Task PlayCardAsync(string roomId, Card card, BoardSide side,
                              CancellationToken ct = default) =>
        _client.SendAsync(new GamePlay
        {
            RoomId    = roomId,
            Card      = card,
            BoardSide = side
        }, ct);

    /// <summary>
    /// Pass → <see cref="GamePass"/>
    /// </summary>
    public Task PassAsync(string roomId, CancellationToken ct = default) =>
        _client.SendAsync(new GamePass { RoomId = roomId }, ct);

    // ── Disposal ──────────────────────────────────────────────────────────────
    public async ValueTask DisposeAsync()
    {
        await _client.DisconnectAsync().ConfigureAwait(false);
        Dispatcher.Dispose();
        _client.Dispose();
    }
}
