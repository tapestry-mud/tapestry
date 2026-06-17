using Tapestry.Contracts;
using Tapestry.Engine.Help;

namespace Tapestry.Server.Gmcp.Handlers;

public class CommandCategoriesHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly HelpService _helpService;

    public string Name => "CommandCategories";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Commands.Categories" };

    public CommandCategoriesHandler(IGmcpConnectionManager connectionManager, HelpService helpService)
    {
        _connectionManager = connectionManager;
        _helpService = helpService;
    }

    public void Configure() { }

    public void SendBurst(string connectionId, object entity)
    {
        var categories = _helpService.VisibleDeclaredCategories
            .Select(c => new { id = c.Id, label = c.Label })
            .ToList();
        _connectionManager.Send(connectionId, "Commands.Categories", new { categories });
    }
}
