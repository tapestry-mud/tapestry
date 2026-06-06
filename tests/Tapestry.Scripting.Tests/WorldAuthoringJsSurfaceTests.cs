// tests/Tapestry.Scripting.Tests/WorldAuthoringJsSurfaceTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
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
        var projector = new RoomProjector(_world, props, tags);
        var writer = new AttributeWriter(props, tags);
        _mod = new WorldAuthoringModule(
            _world, projector, writer, _root, new HashSet<string> { "legends-forgotten" },
            new AreaRegistry());

        _runtime = new JintRuntime(
            new IJintApiModule[] { _mod },
            NullLogger<JintRuntime>.Instance);
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

        var renamed = _runtime.Evaluate(
            "tapestry.authoring.setRoomName('legends-forgotten:lf-test-1', 'The Gatehouse').renamed");
        var id = _runtime.Evaluate(
            "tapestry.authoring.setRoomName('legends-forgotten:gatehouse', 'The Gatehouse').id");
        var warningsLength = _runtime.Evaluate(
            "tapestry.authoring.setRoomName('legends-forgotten:gatehouse', 'The Gatehouse').warnings.length");

        Convert.ToBoolean(renamed).Should().BeTrue("the first rename re-keys");
        id?.ToString().Should().Be("legends-forgotten:gatehouse",
            "renaming to the same name again is a no-op that still reports the current id");
        Convert.ToInt32(warningsLength).Should().Be(0);
    }

    [Fact]
    public void SetRoomName_FromJs_MissingRoom_ReportsNotOk()
    {
        var ok = _runtime.Evaluate(
            "tapestry.authoring.setRoomName('legends-forgotten:nope', 'X').ok");

        Convert.ToBoolean(ok).Should().BeFalse();
    }
}
