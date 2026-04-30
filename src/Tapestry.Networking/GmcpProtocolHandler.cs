using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class GmcpProtocolHandler : IProtocolHandler, IGmcpHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, int> _supportedPackages = new(StringComparer.OrdinalIgnoreCase);
    private bool _supportsSetReceived;
    private TelnetConnection? _connection;

    public byte OptionCode => TelnetProtocolConstants.OPT_GMCP;
    public bool IsSessionLong => true;
    public bool GmcpActive { get; private set; }

    public Action<string, JsonElement>? OnGmcpMessage { get; set; }

    public Task NegotiateAsync(TelnetConnection connection, CancellationToken ct)
    {
        _connection = connection;
        connection.SendRawBytes(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WILL, TelnetProtocolConstants.OPT_GMCP });
        return Task.CompletedTask;
    }

    public void HandleRemoteDo(TelnetConnection connection)
    {
        _connection = connection;
        GmcpActive = true;
    }

    public void SetConnection(TelnetConnection connection)
    {
        _connection = connection;
    }

    public void HandleSubnegotiation(byte[] data)
    {
        if (data.Length == 0) { return; }

        var text = Encoding.UTF8.GetString(data).Trim();
        var spaceIdx = text.IndexOf(' ');
        var package = spaceIdx < 0 ? text : text[..spaceIdx];
        var jsonStr = spaceIdx < 0 ? "null" : text[(spaceIdx + 1)..];

        JsonElement payload;
        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(jsonStr);
        }
        catch
        {
            return;
        }

        if (package.Equals("Core.Supports.Set", StringComparison.OrdinalIgnoreCase))
        {
            ParseSupportsSet(payload);
        }
        else if (package.Equals("Core.Supports.Remove", StringComparison.OrdinalIgnoreCase))
        {
            ParseSupportsRemove(payload);
        }

        OnGmcpMessage?.Invoke(package, payload);
    }

    public bool SupportsPackage(string package)
    {
        if (!_supportsSetReceived) { return true; }
        foreach (var key in _supportedPackages.Keys)
        {
            if (package.Equals(key, StringComparison.OrdinalIgnoreCase)) { return true; }
            if (package.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase)) { return true; }
        }
        return false;
    }

    public void Send(string package, object payload)
    {
        if (_connection == null || !GmcpActive) { return; }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = package + " " + json;
        var bytes = Encoding.UTF8.GetBytes(content);
        _connection.SendSubnegotiation(TelnetProtocolConstants.OPT_GMCP, bytes);
    }

    private void ParseSupportsSet(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array) { return; }
        _supportsSetReceived = true;
        foreach (var item in payload.EnumerateArray())
        {
            var entry = item.GetString();
            if (entry == null) { continue; }
            var parts = entry.Split(' ', 2);
            var name = parts[0];
            var version = parts.Length > 1 && int.TryParse(parts[1], out var v) ? v : 1;
            _supportedPackages[name] = version;
        }
    }

    private void ParseSupportsRemove(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array) { return; }
        foreach (var item in payload.EnumerateArray())
        {
            var entry = item.GetString();
            if (entry == null) { continue; }
            var name = entry.Split(' ', 2)[0];
            _supportedPackages.Remove(name);
        }
    }
}
