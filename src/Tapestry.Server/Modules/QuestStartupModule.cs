using Tapestry.Contracts;
using Tapestry.Engine.Quests;

namespace Tapestry.Server.Modules;

public class QuestStartupModule : IGameModule
{
    private readonly QuestObjectiveWatcher _objectiveWatcher;
    private readonly QuestPersistenceService _persistenceService;
    private readonly QuestScriptCoverageVerifier _coverageVerifier;

    public string Name => "QuestStartup";

    public QuestStartupModule(
        QuestObjectiveWatcher objectiveWatcher,
        QuestPersistenceService persistenceService,
        QuestScriptCoverageVerifier coverageVerifier)
    {
        _objectiveWatcher = objectiveWatcher;
        _persistenceService = persistenceService;
        _coverageVerifier = coverageVerifier;
    }

    public void Configure()
    {
        // Quests are loaded by PackLoader.LoadContent via the quests: glob. Quest JS lifecycle
        // scripts are loaded by the SINGLE executor -- the pack scripts: glob (also PackLoader),
        // with full pack/source attribution. The legacy quest `script:` field no longer drives
        // loading; here we only assert each declared script: was actually loaded by the glob
        // (a strict pack fails the boot, a lenient pack warns), then start persistence + watcher.
        _coverageVerifier.Verify();

        _persistenceService.Start();
        _objectiveWatcher.Start();
    }
}
