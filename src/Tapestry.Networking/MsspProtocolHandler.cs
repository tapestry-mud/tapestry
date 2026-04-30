using System.Text;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class MsspProtocolHandler : IProtocolHandler
{
    private readonly MsspConfig _config;
    private readonly Func<MsspDynamicValues> _getDynamic;

    public byte OptionCode => TelnetProtocolConstants.OPT_MSSP;
    public bool IsSessionLong => false;

    public MsspProtocolHandler(MsspConfig config, Func<MsspDynamicValues> getDynamic)
    {
        _config = config;
        _getDynamic = getDynamic;
    }

    public Task NegotiateAsync(TelnetConnection connection, CancellationToken ct)
    {
        connection.SendRawBytes(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WILL, TelnetProtocolConstants.OPT_MSSP });
        return Task.CompletedTask;
    }

    public void HandleRemoteDo(TelnetConnection connection)
    {
        var table = BuildVariableTable();
        connection.SendSubnegotiation(TelnetProtocolConstants.OPT_MSSP, table);
    }

    public void HandleSubnegotiation(byte[] data)
    {
        // MSSP never receives subneg from client - no-op
    }

    private byte[] BuildVariableTable()
    {
        var dyn = _getDynamic();
        var vars = new List<(string name, string value)>
        {
            ("NAME",         _config.Name),
            ("CODEBASE",     _config.Codebase),
            ("CONTACT",      _config.Contact),
            ("HOSTNAME",     _config.Hostname),
            ("PORT",         _config.Port.ToString()),
            ("CREATED",      _config.Created),
            ("LANGUAGE",     _config.Language),
            ("FAMILY",       _config.Family),
            ("GAMEPLAY",     string.Join("|", _config.Gameplay)),
            ("CLASSES",      _config.Classes      ? "1" : "0"),
            ("RACES",        _config.Races        ? "1" : "0"),
            ("LEVELS",       _config.Levels       ? "1" : "0"),
            ("EQUIPMENT",    _config.Equipment    ? "1" : "0"),
            ("MULTIPLAYING", _config.Multiplaying ? "1" : "0"),
            ("PLAYERKILLING",_config.PlayerKilling? "1" : "0"),
            ("PLAYERS",      dyn.Players.ToString()),
            ("UPTIME",       dyn.UptimeEpoch.ToString()),
            ("ANSI",         "1"),
            ("UTF-8",        "1"),
            ("GMCP",         "1"),
            ("MCCP",         "0"),
        };

        var result = new List<byte>();
        foreach (var (name, value) in vars)
        {
            result.Add(TelnetProtocolConstants.MSSP_VAR);
            result.AddRange(Encoding.ASCII.GetBytes(name));
            result.Add(TelnetProtocolConstants.MSSP_VAL);
            result.AddRange(Encoding.ASCII.GetBytes(value));
        }
        return result.ToArray();
    }
}
