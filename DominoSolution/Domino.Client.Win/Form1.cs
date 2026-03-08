using System;
using System.Windows.Forms;
using Domino.Client.Win.Network;
using Domino.Shared.Enums;
using Domino.Shared.Messages;
using Domino.Shared.Models;

namespace Domino.Client.Win;

/// <summary>
/// Example showing how to wire <see cref="NetworkService"/> inside a WinForms Form.
/// Drop this pattern into Form1 (or any child form) — replace the stub controls
/// with your real designer-generated fields.
/// </summary>
public partial class Form1 : Form
{
    private readonly NetworkService _net;

    // Tracks the room the local player is currently in
    private string? _currentRoomId;
    // Populated after a successful LoginResponse
    private string? _localPlayerId;

    public Form1()
    {
        InitializeComponent();

        // NetworkService must be created on the UI thread so
        // TcpNetworkClient captures the correct SynchronizationContext.
        _net = new NetworkService();

        // ── Server → Client events (already on UI thread) ─────────────────
        _net.Dispatcher.OnLoginResponse += OnLoginResponse;
        _net.Dispatcher.OnRoomsList     += OnRoomsList;
        _net.Dispatcher.OnGameStart     += OnGameStart;
        _net.Dispatcher.OnGameState     += OnGameState;
        _net.Dispatcher.OnGameEnd       += OnGameEnd;
        _net.Dispatcher.OnUnhandledMessage += (_, raw) => SetStatus($"[?] {raw}");

        // ── Connection lifecycle ───────────────────────────────────────────
        _net.OnConnectionStateChanged += OnConnectionStateChanged;
    }

    // ── Form events ───────────────────────────────────────────────────────────

    private async void Form1_Load(object sender, EventArgs e)
    {
        SetStatus("Connecting…");
        try
        {
            await _net.ConnectAsync("localhost", 5000);
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}");
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        await _net.DisposeAsync();
        base.OnFormClosing(e);
    }

    // ── UI → Network (button click handlers) ──────────────────────────────────

    /// <summary>Login button — sends <see cref="LoginRequest"/></summary>
    private async void btnLogin_Click(object sender, EventArgs e) =>
        await SafeSend(() => _net.LoginAsync(txtPlayerName.Text.Trim()));

    /// <summary>Create Room button — sends <see cref="RoomCreateRequest"/></summary>
    private async void btnCreateRoom_Click(object sender, EventArgs e) =>
        await SafeSend(() => _net.CreateRoomAsync(
            name:       txtRoomName.Text.Trim(),
            capacity:   (int)nudCapacity.Value,
            scoreLimit: (int)nudScoreLimit.Value));

    /// <summary>Join Room button — sends <see cref="RoomJoinRequest"/></summary>
    private async void btnJoinRoom_Click(object sender, EventArgs e)
    {
        if (lstRooms.SelectedItem is Room room)
            await SafeSend(() => _net.JoinRoomAsync(room.Id));
    }

    /// <summary>Leave Room button — sends <see cref="RoomLeaveRequest"/></summary>
    private async void btnLeaveRoom_Click(object sender, EventArgs e)
    {
        if (_currentRoomId is not null)
            await SafeSend(() => _net.LeaveRoomAsync(_currentRoomId));
    }

    /// <summary>Play Card button — sends <see cref="GamePlay"/></summary>
    private async void btnPlayCard_Click(object sender, EventArgs e)
    {
        if (_currentRoomId is null || lstHand.SelectedItem is not Card card) return;

        // BoardSide chosen by a radio button or toggle in the real UI
        BoardSide side = rbLeft.Checked ? BoardSide.Left : BoardSide.Right;
        await SafeSend(() => _net.PlayCardAsync(_currentRoomId, card, side));
    }

    /// <summary>Pass button — sends <see cref="GamePass"/></summary>
    private async void btnPass_Click(object sender, EventArgs e)
    {
        if (_currentRoomId is not null)
            await SafeSend(() => _net.PassAsync(_currentRoomId));
    }

    // ── Network → UI handlers ─────────────────────────────────────────────────

    private void OnLoginResponse(object? sender, LoginResponse r)
    {
        _localPlayerId = r.PlayerId;
        SetStatus($"Logged in — player id: {r.PlayerId}");
        // Enable lobby controls, hide login panel, etc.
    }

    private void OnRoomsList(object? sender, RoomsList msg)
    {
        lstRooms.Items.Clear();
        foreach (Room room in msg.Rooms)
            lstRooms.Items.Add(room); // override Room.ToString() or use DisplayMember
    }

    private void OnGameStart(object? sender, GameStart msg)
    {
        _currentRoomId = msg.RoomId;
        SetStatus($"Game started in room {msg.RoomId}");
        // Switch to game panel
    }

    private void OnGameState(object? sender, GameState msg)
    {
        GameSnapshot snap = msg.Snapshot;

        lblCurrentTurn.Text = snap.CurrentPlayerId == _localPlayerId
            ? "Your turn!"
            : $"Waiting for {snap.CurrentPlayerId}…";

        lblBoneyard.Text = $"Boneyard: {snap.BoneyardCount}";

        // Populate hand for local player
        lstHand.Items.Clear();
        if (_localPlayerId is not null &&
            snap.HandsByPlayerId.TryGetValue(_localPlayerId, out var hand))
        {
            foreach (Card card in hand)
                lstHand.Items.Add(card); // override Card.ToString() → e.g. "[3|4]"
        }

        // Enable/disable play & pass buttons
        bool myTurn = snap.CurrentPlayerId == _localPlayerId;
        btnPlayCard.Enabled = myTurn;
        btnPass.Enabled     = myTurn;
    }

    private void OnGameEnd(object? sender, GameEnd msg)
    {
        string scores = string.Empty;
        foreach (var (playerId, score) in msg.FinalScores)
            scores += $"\n  {playerId}: {score}";

        MessageBox.Show($"Game over!\nRoom: {msg.RoomId}\nScores:{scores}",
            "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _currentRoomId = null;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        SetStatus(e.State switch
        {
            ConnectionState.Connected    => "Connected",
            ConnectionState.Connecting   => "Connecting…",
            ConnectionState.Reconnecting => $"Reconnecting… ({e.Error?.Message})",
            ConnectionState.Disconnected => "Disconnected",
            _                            => e.State.ToString()
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task SafeSend(Func<System.Threading.Tasks.Task> action)
    {
        try   { await action(); }
        catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
    }

    private void SetStatus(string text)
    {
        // Works even if called from a non-UI thread (belt-and-suspenders).
        if (InvokeRequired) Invoke(() => lblStatus.Text = text);
        else                lblStatus.Text = text;
    }

    // ── Designer stub controls ────────────────────────────────────────────────
    // In the real project these live in Form1.Designer.cs — listed here for reference.
#pragma warning disable CS0649
    private TextBox      txtPlayerName  = null!;
    private TextBox      txtRoomName    = null!;
    private NumericUpDown nudCapacity   = null!;
    private NumericUpDown nudScoreLimit = null!;
    private ListBox      lstRooms       = null!;
    private ListBox      lstHand        = null!;
    private Label        lblCurrentTurn = null!;
    private Label        lblBoneyard    = null!;
    private Label        lblStatus      = null!;
    private Button       btnLogin       = null!;
    private Button       btnCreateRoom  = null!;
    private Button       btnJoinRoom    = null!;
    private Button       btnLeaveRoom   = null!;
    private Button       btnPlayCard    = null!;
    private Button       btnPass        = null!;
    private RadioButton  rbLeft         = null!;
    private RadioButton  rbRight        = null!;
#pragma warning restore CS0649
}
