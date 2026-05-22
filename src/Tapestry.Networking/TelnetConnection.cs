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
    private bool _disconnectFired;

    // Telnet parse state machine. State persists across ReadAsync calls so that
    // IAC sequences split across read-buffer boundaries are handled correctly.
    private enum ParseState { Normal, Iac, Negotiate, SubnegOption, Subneg, SubnegIac }
    private ParseState _parseState = ParseState.Normal;
    private byte _subnegOption;
    private readonly List<byte> _subnegBuffer = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => _client.Connected;
    public bool SupportsAnsi => Capabilities.ColorSupport != ColorSupport.None;
    public string? RemoteAddress { get; }

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
        RemoteAddress = client.Client.RemoteEndPoint?.ToString()?.Split(':')[0];
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
        if (_disconnectFired) { return; }
        _disconnectFired = true;

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

                    // --- Telnet command parsing (state persists across reads) ---
                    switch (_parseState)
                    {
                        case ParseState.Iac:
                            // We previously saw IAC; b is the command byte.
                            if (b == TelnetProtocolConstants.SB)
                            {
                                _parseState = ParseState.SubnegOption;
                            }
                            else if (b >= TelnetProtocolConstants.WILL && b <= TelnetProtocolConstants.DONT)
                            {
                                _parseState = ParseState.Negotiate; // option byte follows
                            }
                            else
                            {
                                _parseState = ParseState.Normal; // 2-byte IAC command (incl. IAC IAC); ignore
                            }
                            continue;

                        case ParseState.Negotiate:
                            // Option byte for IAC WILL/WONT/DO/DONT; consume and ignore.
                            _parseState = ParseState.Normal;
                            continue;

                        case ParseState.SubnegOption:
                            _subnegOption = b;
                            _subnegBuffer.Clear();
                            _parseState = ParseState.Subneg;
                            continue;

                        case ParseState.Subneg:
                            if (b == TelnetProtocolConstants.IAC)
                            {
                                _parseState = ParseState.SubnegIac;
                            }
                            else
                            {
                                _subnegBuffer.Add(b);
                            }
                            continue;

                        case ParseState.SubnegIac:
                            if (b == TelnetProtocolConstants.SE)
                            {
                                Router?.HandleSubnegotiation(_subnegOption, _subnegBuffer.ToArray());
                                _subnegBuffer.Clear();
                                _parseState = ParseState.Normal;
                            }
                            else if (b == TelnetProtocolConstants.IAC)
                            {
                                _subnegBuffer.Add(TelnetProtocolConstants.IAC); // escaped IAC
                                _parseState = ParseState.Subneg;
                            }
                            else
                            {
                                // Stray IAC command inside subneg; treat byte as data.
                                _subnegBuffer.Add(b);
                                _parseState = ParseState.Subneg;
                            }
                            continue;
                    }

                    if (b == TelnetProtocolConstants.IAC)
                    {
                        _parseState = ParseState.Iac;
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
                        OnInput?.Invoke(line);
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
