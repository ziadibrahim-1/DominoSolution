using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Client.Win.Network;

/// <summary>
/// Abstraction for the TCP network transport layer.
/// </summary>
public interface INetworkClient : IDisposable
{
    /// <summary>
    /// Raised on the UI thread whenever a raw JSON line arrives from the server.
    /// Wire this to <see cref="MessageDispatcher"/> rather than consuming it directly.
    /// </summary>
    event EventHandler<string> OnRawMessage;

    /// <summary>Raised on the UI thread when the connection state changes.</summary>
    event EventHandler<ConnectionStateChangedEventArgs> OnConnectionStateChanged;

    bool IsConnected { get; }

    Task ConnectAsync(string host, int port, CancellationToken ct = default);
    Task DisconnectAsync();

    /// <summary>Serialises <typeparamref name="TMessage"/> as newline-delimited JSON and sends it.</summary>
    Task SendAsync<TMessage>(TMessage message, CancellationToken ct = default);
}

public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }

public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState State { get; }
    public Exception?      Error { get; }

    public ConnectionStateChangedEventArgs(ConnectionState state, Exception? error = null)
    {
        State = state;
        Error = error;
    }
}
