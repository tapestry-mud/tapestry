using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Engine.Color;

namespace Tapestry.Server.Gmcp.Handlers;

public class DisplayHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly ThemeRegistry _themeRegistry;

    public string Name => "Display";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "World.Display.Colors" };

    public DisplayHandler(IGmcpConnectionManager connectionManager, ThemeRegistry themeRegistry)
    {
        _connectionManager = connectionManager;
        _themeRegistry = themeRegistry;
    }

    public void Configure() { }

    public void SendBurst(string connectionId, object entity)
    {
        var colors = _themeRegistry.GetHtmlMap();
        _connectionManager.Send(connectionId, "World.Display.Colors", new { colors });
    }
}
