using Tapestry.Shared;

namespace Tapestry.Engine;

public sealed class AsyncConnectionAdapter : IDisposable
{
    /// <summary>Maximum number of input lines buffered while no read is pending.</summary>
    public const int MaxBufferedInputLines = 64;

    private readonly IConnection _connection;
    private TaskCompletionSource<string>? _pending;
    private readonly Queue<string> _buffer = new();
    private readonly object _lock = new();

    public string ConnectionId => _connection.Id;
    public bool IsConnected => _connection.IsConnected;

    public AsyncConnectionAdapter(IConnection connection)
    {
        _connection = connection;
        _connection.OnInput += HandleInput;
        _connection.OnDisconnected += HandleDisconnect;
    }

    public Task<string> ReadLineAsync(CancellationToken ct)
    {
        TaskCompletionSource<string> tcs;

        lock (_lock)
        {
            // Deliver any input that arrived before this read was requested.
            if (_buffer.Count > 0)
            {
                return Task.FromResult(_buffer.Dequeue());
            }

            tcs = new TaskCompletionSource<string>();
            _pending = tcs;
        }

        ct.Register(() =>
        {
            lock (_lock)
            {
                if (_pending == tcs)
                {
                    _pending = null;
                }
            }
            tcs.TrySetCanceled(ct);
        });

        return tcs.Task;
    }

    private void HandleInput(string input)
    {
        TaskCompletionSource<string>? tcs;
        lock (_lock)
        {
            tcs = _pending;
            _pending = null;

            // No reader waiting: buffer the line (bounded) so it isn't lost to a
            // race between the prompt being written and the next ReadLineAsync.
            if (tcs == null)
            {
                if (_buffer.Count < MaxBufferedInputLines)
                {
                    _buffer.Enqueue(input);
                }
                return;
            }
        }
        tcs.TrySetResult(input);
    }

    private void HandleDisconnect()
    {
        TaskCompletionSource<string>? tcs;
        lock (_lock)
        {
            tcs = _pending;
            _pending = null;
        }
        tcs?.TrySetCanceled();
    }

    public void SendLine(string text) { _connection.SendLine(text); }
    public void SuppressEcho() { _connection.SuppressEcho(); }
    public void RestoreEcho() { _connection.RestoreEcho(); }
    public void Disconnect(string reason) { _connection.Disconnect(reason); }

    public void Dispose()
    {
        _connection.OnInput -= HandleInput;
        _connection.OnDisconnected -= HandleDisconnect;

        TaskCompletionSource<string>? tcs;
        lock (_lock)
        {
            tcs = _pending;
            _pending = null;
        }
        tcs?.TrySetCanceled();
    }
}
