using Microsoft.Extensions.Logging;
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
    private readonly JintRuntime _jintRuntime;
    private readonly QuestPersistenceService _persistenceService;
    private readonly ILogger<QuestStartupModule> _logger;

    public string Name => "QuestStartup";

    public QuestStartupModule(
        ServerConfig config,
        PackLoader packLoader,
        QuestRegistry questRegistry,
        QuestObjectiveWatcher objectiveWatcher,
        JintRuntime jintRuntime,
        QuestPersistenceService persistenceService,
        ILogger<QuestStartupModule> logger)
    {
        _config = config;
        _packLoader = packLoader;
        _questRegistry = questRegistry;
        _objectiveWatcher = objectiveWatcher;
        _jintRuntime = jintRuntime;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    public void Configure()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");
        var packDirs = _config.Packs
            .Where(packName => _packLoader.LoadedPacks.Any(p => p.Name == packName))
            .Select(packName => Path.Combine(packsDir, packName))
            .ToList();

        _questRegistry.Load(packDirs);

        // Load quest lifecycle scripts
        foreach (var quest in _questRegistry.All().Where(q => q.Script != null && q.PackDirectory != null))
        {
            var scriptPath = Path.Combine(quest.PackDirectory!, quest.Script!);
            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("Quest script not found: {Path}", scriptPath);
                continue;
            }
            try
            {
                _jintRuntime.Execute(File.ReadAllText(scriptPath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load quest script: {Path}", scriptPath);
            }
        }

        // Start persistence (subscribes to player.login)
        _persistenceService.Start();

        _objectiveWatcher.Start();
    }
}
