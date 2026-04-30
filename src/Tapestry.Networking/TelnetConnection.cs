using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class TelnetConnection : IConnection
{
    public const int MaxLineLength = 4096;
    public const int MaxBufferSize = 65536;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ILogger _logger;
    private readonly StringBuilder _inputBuffer = new();
    private bool _echoEnabled = true;

    // Subneg state machine fields
    private bool _inSubneg;
    private byte _subnegOption;
    private readonly List<byte> _subnegBuffer = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => _client.Connected;
    public bool SupportsAnsi => Capabilities.ColorSupport != ColorSupport.None;
    public ClientCapabilities Capabilities { get; private set; } = ClientCapabilities.Default;
    public TelnetProtocolRouter? Router { get; private set; }

    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public TelnetConnection(TcpClient client, ILogger logger)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _logger = logger;
    }

    public NetworkStream GetStream() => _stream;

    public void AttachRouter(TelnetProtocolRouter router)
    {
        Router = router;
    }

    public void SetCapabilities(ClientCapabilities caps)
    {
        Capabilities = caps;

        if (caps.UseServerEcho)
        {
            SendRawBytes(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WILL, TelnetProtocolConstants.OPT_ECHO });
            _echoEnabled = true;
        }
        else
        {
            _echoEnabled = false;
        }
    }

    public void SendSubnegotiation(byte optionCode, byte[] data)
    {
        var escaped = EscapeIac(data);
        var frame = new byte[escaped.Length + 5];
        frame[0] = 255; // IAC
        frame[1] = 250; // SB
        frame[2] = optionCode;
        Buffer.BlockCopy(escaped, 0, frame, 3, escaped.Length);
        frame[escaped.Length + 3] = 255; // IAC
        frame[escaped.Length + 4] = 240; // SE
        SendRawBytes(frame);
    }

    private static byte[] EscapeIac(byte[] data)
    {
        if (!data.Contains((byte)255)) { return data; }
        var result = new List<byte>(data.Length + 4);
        foreach (var b in data)
        {
            result.Add(b);
            if (b == 255) { result.Add(255); }
        }
        return result.ToArray();
    }

    public void SendText(string text)
    {
        if (!IsConnected) { return; }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending to {ConnectionId}", Id);
            Disconnect("Write error");
        }
    }

    public void SendLine(string text)
    {
        SendText(text + "\r\n");
    }

    public void SuppressEcho()
    {
        if (Capabilities.UseServerEcho)
        {
            _echoEnabled = false;
        }
        else
        {
            SendRawBytes(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WILL, TelnetProtocolConstants.OPT_ECHO });
        }
    }

    public void RestoreEcho()
    {
        if (Capabilities.UseServerEcho)
        {
            _echoEnabled = true;
        }
        else
        {
            SendRawBytes(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WONT, TelnetProtocolConstants.OPT_ECHO });
        }
    }

    public void ClearScreen()
    {
        if (SupportsAnsi) { SendText("\x1b[2J\x1b[H"); }
    }

    internal void SendRawBytes(byte[] bytes)
    {
        if (!IsConnected) { return; }

        try
        {
            _stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending raw bytes to {ConnectionId}", Id);
        }
    }

    public void Disconnect(string reason)
    {
        _logger.LogInformation("Connection {Id} disconnected: {Reason}", Id, reason);
        try { _client.Close(); }
        catch { }
        Router?.Dispose();
        OnDisconnectedWithReason?.Invoke(reason);
        OnDisconnected?.Invoke();
    }

    public async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1024];
        try
        {
            while (!ct.IsCancellationRequested && IsConnected)
            {
                var bytesRead = await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    Disconnect("connection closed");
                    return;
                }

                if (_inputBuffer.Length > MaxBufferSize)
                {
                    _logger.LogWarning("Input buffer exceeded {MaxSize} bytes, disconnecting connection {Id}", MaxBufferSize, Id);
                    Disconnect("input buffer overflow");
                    return;
                }

                for (var i = 0; i < bytesRead; i++)
                {
                    var b = buffer[i];

                    // --- Subneg state machine ---
                    if (_inSubneg)
                    {
                        if (b == TelnetProtocolConstants.IAC && i + 1 < bytesRead && buffer[i + 1] == TelnetProtocolConstants.SE)
                        {
                            _inSubneg = false;
                            Router?.HandleSubnegotiation(_subnegOption, _subnegBuffer.ToArray());
                            _subnegBuffer.Clear();
                            i++; // skip the SE byte
                        }
                        else
                        {
                            _subnegBuffer.Add(b);
                        }
                        continue;
                    }

                    // --- IAC sequence handling ---
                    if (b == TelnetProtocolConstants.IAC && i + 1 < bytesRead)
                    {
                        var cmd = buffer[i + 1];

                        if (cmd == TelnetProtocolConstants.SB) // start subneg
                        {
                            if (i + 2 < bytesRead)
                            {
                                _subnegOption = buffer[i + 2];
                                _subnegBuffer.Clear();
                                _inSubneg = true;
                                i += 2; // advance past IAC SB option
                            }
                            else
                            {
                                i += 1; // incomplete - skip
                            }
                            continue;
                        }

                        if (cmd >= TelnetProtocolConstants.WILL && cmd <= TelnetProtocolConstants.DONT && i + 2 < bytesRead)
                        {
                            i += 2; // skip 3-byte command
                            continue;
                        }

                        i += 1; // skip 2-byte IAC command
                        continue;
                    }

                    // --- Regular input ---
                    if (b == '\n')
                    {
                        if (_echoEnabled)
                        {
                            SendRawBytes(new byte[] { (byte)'\r', (byte)'\n' });
                        }
                        var line = _inputBuffer.ToString().TrimEnd('\r');
                        _inputBuffer.Clear();
                        if (line.Length > 0) { OnInput?.Invoke(line); }
                    }
                    else if (b == '\r')
                    {
                        // Ignore CR; handle on LF
                    }
                    else if (b == 8 || b == 127)
                    {
                        if (_inputBuffer.Length > 0)
                        {
                            _inputBuffer.Length--;
                            if (_echoEnabled)
                            {
                                SendRawBytes(new byte[] { 8, (byte)' ', 8 });
                            }
                        }
                    }
                    else if (b >= 32)
                    {
                        _inputBuffer.Append((char)b);
                        if (_echoEnabled) { SendRawBytes(new byte[] { b }); }

                        if (_inputBuffer.Length > MaxLineLength)
                        {
                            _logger.LogWarning("Input line exceeded {MaxLength} bytes, clearing buffer for connection {Id}", MaxLineLength, Id);
                            _inputBuffer.Clear();
                            SendLine("Input too long, discarded.");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Disconnect("server shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Read error on connection {Id}: {Error}", Id, ex.Message);
            Disconnect("read error: " + ex.Message);
        }
    }
}
