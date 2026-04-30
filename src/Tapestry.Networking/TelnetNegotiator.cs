using System.Text;

namespace Tapestry.Networking;

public class TelnetNegotiator
{
    private static readonly byte[] DoTtypeAndDoNaws =
    {
        TelnetProtocolConstants.IAC, TelnetProtocolConstants.DO, TelnetProtocolConstants.OPT_TTYPE,
        TelnetProtocolConstants.IAC, TelnetProtocolConstants.DO, TelnetProtocolConstants.OPT_NAWS
    };

    private static readonly byte[] SbTtypeSend =
    {
        TelnetProtocolConstants.IAC, TelnetProtocolConstants.SB, TelnetProtocolConstants.OPT_TTYPE,
        1, // TTYPE SEND
        TelnetProtocolConstants.IAC, TelnetProtocolConstants.SE
    };

    private readonly int _timeoutMs;
    private readonly IReadOnlyList<IProtocolHandler> _handlers;

    public TelnetNegotiator(int timeoutMs = 500, IReadOnlyList<IProtocolHandler>? handlers = null)
    {
        _timeoutMs = timeoutMs;
        _handlers = handlers ?? Array.Empty<IProtocolHandler>();
    }

    public async Task<ClientCapabilities> NegotiateAsync(TelnetConnection connection, CancellationToken ct)
    {
        var stream = connection.GetStream();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeoutMs);
        var token = timeoutCts.Token;

        string? ttype = null;
        int? windowWidth = null;
        int? windowHeight = null;
        bool sentTtypeSend = false;
        bool gotTtypeValue = false;
        bool gotNawsData = false;
        bool gmcpActive = false;

        var handlersByOption = new Dictionary<byte, IProtocolHandler>();
        foreach (var h in _handlers)
        {
            handlersByOption[h.OptionCode] = h;
        }

        try
        {
            await stream.WriteAsync(DoTtypeAndDoNaws, token);
            await stream.FlushAsync(token);

            foreach (var h in _handlers)
            {
                await h.NegotiateAsync(connection, token);
            }

            var buffer = new byte[256];
            var parseBuffer = new List<byte>();

            while (!token.IsCancellationRequested)
            {
                if (gotTtypeValue && gotNawsData) { break; }

                var bytesRead = await stream.ReadAsync(buffer, token);
                if (bytesRead == 0) { break; }

                for (int i = 0; i < bytesRead; i++)
                {
                    parseBuffer.Add(buffer[i]);
                }

                while (parseBuffer.Count > 0)
                {
                    if (parseBuffer[0] != TelnetProtocolConstants.IAC)
                    {
                        parseBuffer.RemoveAt(0);
                        continue;
                    }

                    if (parseBuffer.Count < 2) { break; }

                    var command = parseBuffer[1];

                    if (command == TelnetProtocolConstants.WILL || command == TelnetProtocolConstants.WONT
                        || command == TelnetProtocolConstants.DO || command == TelnetProtocolConstants.DONT)
                    {
                        if (parseBuffer.Count < 3) { break; }

                        var option = parseBuffer[2];
                        parseBuffer.RemoveRange(0, 3);

                        if (command == TelnetProtocolConstants.WILL && option == TelnetProtocolConstants.OPT_TTYPE)
                        {
                            if (!sentTtypeSend)
                            {
                                sentTtypeSend = true;
                                await stream.WriteAsync(SbTtypeSend, token);
                                await stream.FlushAsync(token);
                            }
                        }

                        if (command == TelnetProtocolConstants.DO && handlersByOption.TryGetValue(option, out var doHandler))
                        {
                            doHandler.HandleRemoteDo(connection);
                            if (option == TelnetProtocolConstants.OPT_GMCP) { gmcpActive = true; }
                        }
                    }
                    else if (command == TelnetProtocolConstants.SB)
                    {
                        int seIndex = -1;
                        for (int j = 2; j < parseBuffer.Count - 1; j++)
                        {
                            if (parseBuffer[j] == TelnetProtocolConstants.IAC && parseBuffer[j + 1] == TelnetProtocolConstants.SE)
                            {
                                seIndex = j;
                                break;
                            }
                        }

                        if (seIndex == -1) { break; }

                        var sbData = parseBuffer.GetRange(2, seIndex - 2).ToArray();
                        parseBuffer.RemoveRange(0, seIndex + 2);

                        if (sbData.Length > 0)
                        {
                            var option = sbData[0];
                            var payload = sbData.Length > 1 ? sbData[1..] : Array.Empty<byte>();

                            if (option == TelnetProtocolConstants.OPT_TTYPE && sbData.Length >= 2 && sbData[1] == 0)
                            {
                                if (sbData.Length > 2)
                                {
                                    ttype = Encoding.ASCII.GetString(sbData, 2, sbData.Length - 2);
                                }
                                gotTtypeValue = true;
                            }
                            else if (option == TelnetProtocolConstants.OPT_NAWS && sbData.Length >= 5)
                            {
                                windowWidth = (sbData[1] << 8) | sbData[2];
                                windowHeight = (sbData[3] << 8) | sbData[4];
                                gotNawsData = true;
                            }
                            else if (handlersByOption.TryGetValue(option, out var sbHandler))
                            {
                                sbHandler.HandleSubnegotiation(payload);
                            }
                        }
                    }
                    else
                    {
                        parseBuffer.RemoveRange(0, 2);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation - build from whatever we collected
        }

        var router = new TelnetProtocolRouter();
        foreach (var h in _handlers)
        {
            if (h.IsSessionLong) { router.Register(h); }
        }
        connection.AttachRouter(router);

        var hasData = ttype != null || windowWidth.HasValue;
        if (hasData)
        {
            return ClientCapabilities.FromNegotiation(ttype, windowWidth, windowHeight, gmcpActive);
        }

        return ClientCapabilities.FromTimeout();
    }
}
