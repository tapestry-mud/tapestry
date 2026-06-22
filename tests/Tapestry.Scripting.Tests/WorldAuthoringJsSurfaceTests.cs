// tests/Tapestry.Scripting.Tests/WorldAuthoringJsSurfaceTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests;

/// <summary>JS-surface contract test for tapestry.authoring.setRoomName — the
/// { ok, id, renamed, warnings } shape that edit-room.js consumes.</summary>
public class WorldAuthoringJsSurfaceTests : IDisposable
{
    private readonly World _world;
    private readonly string _root;
    private readonly JintRuntime _runtime;
    private readonly WorldAuthoringModule _mod;

    public WorldAuthoringJsSurfaceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "authjs-" + Path.GetRandomFileName());
        _world = new World();
        var props = new PropertyRegistry();
        var tags = new TagRegistry();
        var projector = new RoomProjector(_world, props, tags, new AreaRegistry());
        var writer = new AttributeWriter(props, tags);
        _mod = new WorldAuthoringModule(
            _world, projector, writer, _root, new HashSet<string> { "legends-forgotten" },
            new AreaRegistry(), new StubExitResolver());

        _runtime = new JintRuntime(
            new IJintApiModule[] { _mod },
            NullLogger<JintRuntime>.Instance,
            loader: new TapestryModuleLoader(new PackDependencyGraph()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void SetRoomName_FromJs_ReturnsResultObject()
    {
        _mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");

        var renamed = EsmTest.Eval(_runtime,
            "tapestry.authoring.setRoomName('legends-forgotten:lf-test-1', 'The Gatehouse').renamed");
        var id = EsmTest.Eval(_runtime,
            "tapestry.authoring.setRoomName('legends-forgotten:gatehouse', 'The Gatehouse').id");
        var warningsLength = EsmTest.Eval(_runtime,
            "tapestry.authoring.setRoomName('legends-forgotten:gatehouse', 'The Gatehouse').warnings.length");

        Convert.ToBoolean(renamed).Should().BeTrue("the first rename re-keys");
        id?.ToString().Should().Be("legends-forgotten:gatehouse",
            "renaming to the same name again is a no-op that still reports the current id");
        Convert.ToInt32(warningsLength).Should().Be(0);
    }

    [Fact]
    public void SetRoomName_FromJs_MissingRoom_ReportsNotOk()
    {
        var ok = EsmTest.Eval(_runtime,
            "tapestry.authoring.setRoomName('legends-forgotten:nope', 'X').ok");

        Convert.ToBoolean(ok).Should().BeFalse();
    }

    /// <summary>Proves that getArea returns camelCase keys through Jint.
    /// Raw AreaInfo would expose PascalCase members (Exists/Name/SourcePack),
    /// which resolve to undefined in JS — this test would fail on those.</summary>
    [Fact]
    public void GetArea_FromJs_ReturnsCamelCaseProjection()
    {
        // Use the C# method to create the area so the state is set up through the
        // same module (mirrors SetRoomName_FromJs which uses _mod.CreateRoom).
        _mod.CreateArea("road-to-tar-valon", "The Road to Tar Valon");

        var exists = EsmTest.Eval(_runtime,
            "tapestry.authoring.getArea('road-to-tar-valon').exists");
        var name = EsmTest.Eval(_runtime,
            "tapestry.authoring.getArea('road-to-tar-valon').name");
        var sourcePack = EsmTest.Eval(_runtime,
            "tapestry.authoring.getArea('road-to-tar-valon').sourcePack");

        Convert.ToBoolean(exists).Should().BeTrue("camelCase 'exists' key must resolve (not undefined)");
        name?.ToString().Should().Be("The Road to Tar Valon",
            "camelCase 'name' key must resolve to the created area name");
        // sourcePack is null/undefined for an authored (non-pack) area.
        (sourcePack == null || sourcePack.ToString() == "")
            .Should().BeTrue("sourcePack should be null/undefined for an authored area");
    }

    /// <summary>Proves that getAreas() returns a camelCase-projected array through Jint.
    /// Raw AreaSummary records expose PascalCase members (Id, Name, LevelRange, Provenance),
    /// which resolve to undefined in JS — this test proves the projection is correct.</summary>
    [Fact]
    public void GetAreas_FromJs_ReturnsCamelCaseProjection()
    {
        _mod.CreateArea("tar-valon", "Tar Valon");

        var name = EsmTest.Eval(_runtime,
            "tapestry.authoring.getAreas()[0].name");
        var provenance = EsmTest.Eval(_runtime,
            "tapestry.authoring.getAreas()[0].provenance");
        var levelRange = EsmTest.Eval(_runtime,
            "tapestry.authoring.getAreas()[0].levelRange");

        name?.ToString().Should().Be("Tar Valon",
            "camelCase 'name' key must resolve (not undefined) through Jint");
        provenance?.ToString().Should().Be("[authored]",
            "camelCase 'provenance' key must resolve with the correct tag");
        // levelRange is an int[] — Jint exposes it as an array-like object; it must not be undefined.
        levelRange.Should().NotBeNull("camelCase 'levelRange' key must resolve (not undefined)");
    }

    /// <summary>Proves that getAreaRooms() returns a camelCase-projected array through Jint.
    /// Raw RoomSummary records would expose PascalCase members (Id, Name, Provenance),
    /// which resolve to undefined in JS — this test proves the camelCase projection is correct.</summary>
    [Fact]
    public void GetAreaRooms_FromJs_ReturnsCamelCaseProjection()
    {
        _mod.CreateArea("road-to-tar-valon", "Road to Tar Valon");
        // CreateRoom requires a namespaced roomId; "legends-forgotten" is in loadedPackNamespaces.
        _mod.CreateRoom("road-to-tar-valon", "legends-forgotten:road-track-1", "The Track", "A dusty track.");

        var id = EsmTest.Eval(_runtime,
            "tapestry.authoring.getAreaRooms('road-to-tar-valon')[0].id");
        var name = EsmTest.Eval(_runtime,
            "tapestry.authoring.getAreaRooms('road-to-tar-valon')[0].name");
        var provenance = EsmTest.Eval(_runtime,
            "tapestry.authoring.getAreaRooms('road-to-tar-valon')[0].provenance");

        id?.ToString().Should().Be("legends-forgotten:road-track-1",
            "camelCase 'id' key must resolve (not undefined) through Jint");
        name?.ToString().Should().Be("The Track",
            "camelCase 'name' key must resolve (not undefined) through Jint");
        provenance?.ToString().Should().Be("[authored]",
            "camelCase 'provenance' key must resolve with the correct tag — authored room has a side-car and no source_pack");
    }
}
