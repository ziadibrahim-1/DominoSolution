using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Client.Win.Network;

/// <summary>
/// Newline-delimited JSON over TCP with exponential back-off reconnect.
///
/// <para>
/// All <see cref="OnRawMessage"/> and <see cref="OnConnectionStateChanged"/> events
/// are marshalled back to the <see cref="SynchronizationContext"/> that was current
/// when this instance was constructed — i.e. the WinForms UI thread, as long as you
/// create this object from a Form constructor or Load handler.
/// </para>
/// </summary>
public sealed class TcpNetworkClient : INetworkClient
{
    // ── Wire format ───────────────────────────────────────────────────────────
    private static readonly Encoding Wire = Encoding.UTF8;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = false,
    };

    // ── Retry policy ──────────────────────────────────────────────────────────
    private const int MaxRetries  = 6;
    private const int BaseDelayMs = 500;
    private const int MaxDelayMs  = 30_000;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly SynchronizationContext? _uiContext;

    private string? _host;
    private int     _port;

    private TcpClient?              _tcp;
    private StreamWriter?           _writer;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim  _sendLock = new(1, 1);

    private int           _retryCount;
    private volatile bool _intentionalDisconnect;

    // ── Public API ────────────────────────────────────────────────────────────
    public event EventHandler<string>?                       OnRawMessage;
    public event EventHandler<ConnectionStateChangedEventArgs>? OnConnectionStateChanged;

    public bool IsConnected => _tcp?.Connected == true;

    public TcpNetworkClient()
    {
        // Capture the WinForms sync context so events are always raised on the UI thread.
        _uiContext = SynchronizationContext.Current;
    }

    // ── Connect ───────────────────────────────────────────────────────────────
    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        _host                  = host;
        _port                  = port;
        _intentionalDisconnect = false;
        _retryCount            = 0;

        await ConnectCoreAsync(ct).ConfigureAwait(false);
    }

    private async Task ConnectCoreAsync(CancellationToken ct)
    {
        RaiseStateChanged(ConnectionState.Connecting);

        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _tcp?.Dispose();
                _tcp = new TcpClient { NoDelay = true };

                await _tcp.ConnectAsync(_host!, _port, ct).ConfigureAwait(false);

                var stream = _tcp.GetStream();
                _writer = new StreamWriter(stream, Wire) { AutoFlush = false, NewLine = "\n" };

                _retryCount = 0;
                RaiseStateChanged(ConnectionState.Connected);

                // Kick off the read loop (owns its own lifetime via _cts).
                _ = ReadLoopAsync(new StreamReader(stream, Wire), _cts.Token);
                return;
            }
            catch (Exception ex) when (!_intentionalDisconnect)
            {
                _retryCount++;
                if (_retryCount > MaxRetries)
                {
                    RaiseStateChanged(ConnectionState.Disconnected, ex);
                    throw;
                }

                int delay = Math.Min(BaseDelayMs * (1 << (_retryCount - 1)), MaxDelayMs);
                RaiseStateChanged(ConnectionState.Reconnecting, ex);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    // ── Disconnect ────────────────────────────────────────────────────────────
    public async Task DisconnectAsync()
    {
        _intentionalDisconnect = true;
        _cts?.Cancel();
        _tcp?.Close();
        await Task.CompletedTask;
        RaiseStateChanged(ConnectionState.Disconnected);
    }

    // ── Send ──────────────────────────────────────────────────────────────────
    public async Task SendAsync<TMessage>(TMessage message, CancellationToken ct = default)
    {
        if (_writer is null || !IsConnected)
            throw new InvalidOperationException("Not connected to server.");

        string json = JsonSerializer.Serialize(message, JsonOpts);

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
            await _writer.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // ── Read loop (async stream) ──────────────────────────────────────────────
    private async Task ReadLoopAsync(StreamReader reader, CancellationToken ct)
    {
        try
        {
            await foreach (string line in ReadLinesAsync(reader, ct))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Post(() => OnRawMessage?.Invoke(this, line));
            }
        }
        catch (OperationCanceledException) { /* intentional shutdown */ }
        catch (Exception ex)
        {
            if (!_intentionalDisconnect)
            {
                RaiseStateChanged(ConnectionState.Reconnecting, ex);
                _ = Task.Run(() => ConnectCoreAsync(CancellationToken.None), CancellationToken.None);
            }
        }
    }

    private static async IAsyncEnumerable<string>
        ReadLinesAsync(StreamReader reader,
            [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) yield break; // server closed connection
            yield return line;
        }
    }

    // ── Thread marshalling ────────────────────────────────────────────────────
    private void RaiseStateChanged(ConnectionState state, Exception? error = null) =>
        Post(() => OnConnectionStateChanged?.Invoke(this,
                   new ConnectionStateChangedEventArgs(state, error)));

    /// <summary>Posts to the captured UI context; falls back to inline execution.</summary>
    private void Post(Action action)
    {
        if (_uiContext is not null)
            _uiContext.Post(_ => action(), null);
        else
            action();
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        _intentionalDisconnect = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _writer?.Dispose();
        _tcp?.Dispose();
        _sendLock.Dispose();
    }
}
