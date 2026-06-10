using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;

namespace Tapestry.Server.Modules;

public class QuestStartupModule : IGameModule
{
    private readonly QuestRegistry _questRegistry;
    private readonly QuestObjectiveWatcher _objectiveWatcher;
    private readonly JintRuntime _jintRuntime;
    private readonly QuestPersistenceService _persistenceService;
    private readonly ILogger<QuestStartupModule> _logger;

    public string Name => "QuestStartup";

    public QuestStartupModule(
        QuestRegistry questRegistry,
        QuestObjectiveWatcher objectiveWatcher,
        JintRuntime jintRuntime,
        QuestPersistenceService persistenceService,
        ILogger<QuestStartupModule> logger)
    {
        _questRegistry = questRegistry;
        _objectiveWatcher = objectiveWatcher;
        _jintRuntime = jintRuntime;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    public void Configure()
    {
        // Quests are loaded by PackLoader.LoadContent via the quests: glob in each pack manifest.
        // Here we only load the JS lifecycle scripts and start persistence.

        // Load quest lifecycle scripts
        foreach (var quest in _questRegistry.All().Where(q => q.Script != null && q.PackDirectory != null))
        {
            var scriptPath = Path.Combine(quest.PackDirectory!, quest.Script!);
            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("Quest script not found: {Path}", scriptPath);
                continue;
            }

            // The pack `scripts:` glob may already have executed this file (quest scripts
            // routinely live under scripts/). Running it again re-fires registerScript and,
            // under the RegistrationPolicy, trips a same-pack quest-hook collision. Skip any
            // file already executed; MarkFileExecuted returns false when that's the case.
            if (!_jintRuntime.MarkFileExecuted(scriptPath))
            {
                _logger.LogDebug("Quest script already loaded by the scripts glob, skipping: {Path}", scriptPath);
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
