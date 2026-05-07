using Microsoft.Extensions.Logging;
using Tapestry.Contracts;

namespace Tapestry.Server;

public class GameBootstrapper
{
    private readonly IEnumerable<IGameModule> _modules;
    private readonly ILogger<GameBootstrapper> _logger;

    public GameBootstrapper(
        IEnumerable<IGameModule> modules,
        ILogger<GameBootstrapper> logger)
    {
        _modules = modules;
        _logger = logger;
    }

    public void Configure()
    {
        foreach (var module in _modules)
        {
            _logger.LogInformation("Configuring module: {Module}", module.Name);
            module.Configure();
        }
    }
}
