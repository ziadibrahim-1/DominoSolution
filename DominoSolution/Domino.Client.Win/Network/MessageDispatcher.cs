using System;
using System.Text.Json;
using Domino.Shared.Messages;

namespace Domino.Client.Win.Network;

/// <summary>
/// Subscribes to <see cref="INetworkClient.OnRawMessage"/>, reads the
/// <c>"type"</c> discriminator, deserialises to the correct
/// <c>Domino.Shared.Messages</c> record and raises a strongly-typed event.
///
/// <para>
/// All events are already on the UI thread because <see cref="TcpNetworkClient"/>
/// marshals them before raising <c>OnRawMessage</c>.
/// </para>
/// </summary>
public sealed class MessageDispatcher : IDisposable
{
    // ── Wire discriminator constants ──────────────────────────────────────────
    // Must match exactly what the server sends in the "type" field.
    public const string TypeLoginResponse    = "LoginResponse";
    public const string TypeRoomsList        = "RoomsList";
    public const string TypeGameStart        = "GameStart";
    public const string TypeGameState        = "GameState";
    public const string TypeGameEnd          = "GameEnd";
    public const string TypeError            = "Error";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly INetworkClient _client;

    // ── Typed server→client events (UI thread) ────────────────────────────────
    public event EventHandler<LoginResponse>? OnLoginResponse;
    public event EventHandler<RoomsList>?     OnRoomsList;
    public event EventHandler<GameStart>?     OnGameStart;
    public event EventHandler<GameState>?     OnGameState;
    public event EventHandler<GameEnd>?       OnGameEnd;

    /// <summary>Fallback for unrecognised or server-side error messages.</summary>
    public event EventHandler<string>?        OnUnhandledMessage;

    public MessageDispatcher(INetworkClient client)
    {
        _client = client;
        _client.OnRawMessage += HandleRaw;
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────
    private void HandleRaw(object? sender, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                OnUnhandledMessage?.Invoke(this, json);
                return;
            }

            string msgType = typeProp.GetString() ?? string.Empty;

            switch (msgType)
            {
                case TypeLoginResponse:
                    OnLoginResponse?.Invoke(this, Deserialize<LoginResponse>(doc.RootElement));
                    break;

                case TypeRoomsList:
                    OnRoomsList?.Invoke(this, Deserialize<RoomsList>(doc.RootElement));
                    break;

                case TypeGameStart:
                    OnGameStart?.Invoke(this, Deserialize<GameStart>(doc.RootElement));
                    break;

                case TypeGameState:
                    OnGameState?.Invoke(this, Deserialize<GameState>(doc.RootElement));
                    break;

                case TypeGameEnd:
                    OnGameEnd?.Invoke(this, Deserialize<GameEnd>(doc.RootElement));
                    break;

                default:
                    OnUnhandledMessage?.Invoke(this, json);
                    break;
            }
        }
        catch (JsonException)
        {
            OnUnhandledMessage?.Invoke(this, json);
        }
    }

    private static T Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(JsonOpts)
            ?? throw new JsonException($"Could not deserialise to {typeof(T).Name}");

    public void Dispose() => _client.OnRawMessage -= HandleRaw;
}
