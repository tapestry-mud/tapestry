using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Server.Modules;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Server.Tests.Modules;

public class QuestScriptCoverageVerifierTests : IDisposable
{
    private readonly string _packDir;

    public QuestScriptCoverageVerifierTests()
    {
        _packDir = Path.Combine(Path.GetTempPath(), "qscv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_packDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_packDir, recursive: true); } catch { /* best effort */ }
    }

    private sealed class FakeManifests : IPackManifestProvider
    {
        public IReadOnlyList<PackManifest> LoadedPacks { get; init; } = new List<PackManifest>();
    }

    private static JintRuntime BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PackDependencyGraph>().Build(new Dictionary<string, List<string>>());
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return rt;
    }

    private string WriteScriptFile(string relative)
    {
        var path = Path.Combine(_packDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "// quest script");
        return path;
    }

    private QuestScriptCoverageVerifier BuildVerifier(QuestRegistry quests, JintRuntime rt, string validation)
    {
        var manifests = new FakeManifests
        {
            LoadedPacks = new List<PackManifest>
            {
                new() { Name = "@test/pack", PackDirectory = _packDir, Validation = validation },
            },
        };
        return new QuestScriptCoverageVerifier(quests, rt, manifests,
            NullLogger<QuestScriptCoverageVerifier>.Instance);
    }

    [Fact]
    public void UncoveredScript_StrictPack_Throws_NamingQuestPackAndPath()
    {
        WriteScriptFile("quests/q1.js"); // exists on disk, but NOT marked executed by the glob
        var quests = new QuestRegistry();
        quests.Register(new QuestDefinition { Id = "q1", Script = "quests/q1.js", PackDirectory = _packDir });
        var rt = BuildRuntime();
        var verifier = BuildVerifier(quests, rt, validation: "strict");

        var ex = Assert.Throws<InvalidOperationException>(() => verifier.Verify());
        ex.Message.Should().Contain("q1").And.Contain("@test/pack").And.Contain("quests/q1.js");
    }

    [Fact]
    public void MissingScript_StrictPack_Throws()
    {
        // No file written -- the script: path does not exist (typo'd path = dead hook).
        var quests = new QuestRegistry();
        quests.Register(new QuestDefinition { Id = "q1", Script = "quests/nope.js", PackDirectory = _packDir });
        var rt = BuildRuntime();
        var verifier = BuildVerifier(quests, rt, validation: "strict");

        var ex = Assert.Throws<InvalidOperationException>(() => verifier.Verify());
        ex.Message.Should().Contain("q1").And.Contain("does not exist");
    }

    [Fact]
    public void MissingScript_LenientPack_DoesNotThrow()
    {
        // No file written; lenient pack warns instead of failing the boot.
        var quests = new QuestRegistry();
        quests.Register(new QuestDefinition { Id = "q1", Script = "quests/nope.js", PackDirectory = _packDir });
        var rt = BuildRuntime();
        var verifier = BuildVerifier(quests, rt, validation: "lenient");

        var act = () => verifier.Verify();
        act.Should().NotThrow("a validation: lenient pack warns on a missing script: instead of failing the boot");
    }
}
