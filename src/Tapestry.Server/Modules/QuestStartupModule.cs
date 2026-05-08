using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;

namespace Tapestry.Server.Modules;

public class QuestStartupModule : IGameModule
{
    private readonly ServerConfig _config;
    private readonly PackLoader _packLoader;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestObjectiveWatcher _objectiveWatcher;

    public string Name => "QuestStartup";

    public QuestStartupModule(
        ServerConfig config,
        PackLoader packLoader,
        QuestRegistry questRegistry,
        QuestObjectiveWatcher objectiveWatcher)
    {
        _config = config;
        _packLoader = packLoader;
        _questRegistry = questRegistry;
        _objectiveWatcher = objectiveWatcher;
    }

    public void Configure()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");
        var packDirs = _config.Packs
            .Where(packName => _packLoader.LoadedPacks.Any(p => p.Name == packName))
            .Select(packName => Path.Combine(packsDir, packName))
            .ToList();

        _questRegistry.Load(packDirs);
        _objectiveWatcher.Start();
    }
}
