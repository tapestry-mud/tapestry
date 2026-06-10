using Microsoft.Extensions.Logging;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

/// <summary>
/// Asserts that every quest carrying a <c>script:</c> field references a JS file that the
/// pack <c>scripts:</c> glob actually loaded. The glob is the single quest-script executor;
/// a <c>script:</c> naming a file that is missing on disk or simply never loaded by the glob
/// means its hooks can never bind. This is a per-pack content check: a strict pack (the
/// default) fails the boot; a <c>validation: lenient</c> pack only warns -- the same
/// convention as PackValidator.
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
            // Rooted/normalized so File.Exists and the ledger lookup are unambiguous.
            var scriptPath = Path.GetFullPath(Path.Combine(quest.PackDirectory!, quest.Script!));

            var missing = !File.Exists(scriptPath);
            var unglobbed = !missing && !_runtime.HasExecutedFile(scriptPath);

            if (!missing && !unglobbed)
            {
                continue; // exists and the scripts glob already loaded it with correct attribution
            }

            // A missing file is a typo'd script: path -- a guaranteed dead hook, the exact
            // silent failure this check exists to prevent -- so it is gated identically to an
            // unglobbed file, not merely warned.
            var owner = OwnerManifest(quest.PackDirectory!);
            var ownerName = owner?.Name ?? "(unknown)";
            var reason = missing
                ? $"the file '{scriptPath}' does not exist"
                : $"'{scriptPath}' was not loaded by the pack's scripts: glob";
            var message =
                $"Quest '{quest.Id}' (pack '{ownerName}') declares script '{quest.Script}', but " +
                $"{reason}; its hooks can never bind. " +
                "Place the file under the scripts: glob -- it is the only quest-script loader.";

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
