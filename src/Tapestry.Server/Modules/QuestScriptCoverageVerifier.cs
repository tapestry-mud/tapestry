using Microsoft.Extensions.Logging;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

/// <summary>
/// Asserts that every quest carrying a <c>script:</c> field references a JS file that the
/// pack <c>scripts:</c> glob actually loaded. The glob is the single quest-script executor;
/// a <c>script:</c> naming an unloaded file means its hooks can never bind. This is a
/// per-pack content check: a strict pack (the default) fails the boot; a
/// <c>validation: lenient</c> pack only warns -- the same convention as PackValidator.
/// </summary>
public class QuestScriptCoverageVerifier
{
    private readonly QuestRegistry _questRegistry;
    private readonly JintRuntime _runtime;
    private readonly IPackManifestProvider _manifestProvider;
    private readonly ILogger<QuestScriptCoverageVerifier> _logger;

    public QuestScriptCoverageVerifier(
        QuestRegistry questRegistry,
        JintRuntime runtime,
        IPackManifestProvider manifestProvider,
        ILogger<QuestScriptCoverageVerifier> logger)
    {
        _questRegistry = questRegistry;
        _runtime = runtime;
        _manifestProvider = manifestProvider;
        _logger = logger;
    }

    public void Verify()
    {
        foreach (var quest in _questRegistry.All().Where(q => q.Script != null && q.PackDirectory != null))
        {
            var scriptPath = Path.Combine(quest.PackDirectory!, quest.Script!);

            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("Quest script not found: {Path}", scriptPath);
                continue;
            }

            if (_runtime.HasExecutedFile(scriptPath))
            {
                continue; // the scripts glob already loaded it with correct attribution
            }

            var owner = OwnerManifest(quest.PackDirectory!);
            var ownerName = owner?.Name ?? "(unknown)";
            var message =
                $"Quest '{quest.Id}' (pack '{ownerName}') declares script '{quest.Script}', but " +
                $"'{scriptPath}' was not loaded by the pack's scripts: glob; its hooks can never bind. " +
                "Move the file under the scripts: glob -- it is the only quest-script loader.";

            if (owner?.Validation == "lenient")
            {
                _logger.LogWarning("{Message}", message);
            }
            else
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    private PackManifest? OwnerManifest(string packDirectory)
    {
        var full = Path.GetFullPath(packDirectory);
        return _manifestProvider.LoadedPacks.FirstOrDefault(m =>
            !string.IsNullOrEmpty(m.PackDirectory) && Path.GetFullPath(m.PackDirectory) == full);
    }
}
