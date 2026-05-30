using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class WorldTagIndexTests
{
    [Fact]
    public void GetEntitiesByTag_ReturnsEmpty_BeforeAnySwap()
    {
        var world = new World();
        var entity = new Entity("npc", "Goblin");
        entity.AddTag("npc");
        world.TrackEntity(entity);

        world.GetEntitiesByTag("npc").Should().BeEmpty();
    }

    [Fact]
    public void GetEntitiesByTag_AfterSwap_ReturnsTrackedEntityWithTag()
    {
        var world = new World();
        var entity = new Entity("npc", "Goblin");
        entity.AddTag("npc");
        world.TrackEntity(entity);

        world.SwapTagBuffers();

        world.GetEntitiesByTag("npc").Should().Contain(entity);
    }

    [Fact]
    public void SwapTagBuffers_OnEmptyWorld_DoesNotThrow()
    {
        var world = new World();
        world.SwapTagBuffers();
        world.SwapTagBuffers();
        world.GetEntitiesByTag("anything").Should().BeEmpty();
    }

    [Fact]
    public void GetEntitiesByTag_ReflectsTagAddedAfterTrack_OnNextSwap()
    {
        var world = new World();
        var entity = new Entity("npc", "Goblin");
        world.TrackEntity(entity);
        world.SwapTagBuffers();

        entity.AddTag("npc");
        world.GetEntitiesByTag("npc").Should().BeEmpty();

        world.SwapTagBuffers();
        world.GetEntitiesByTag("npc").Should().Contain(entity);
    }

    [Fact]
    public void GetEntitiesByTag_ReflectsTagRemoved_OnNextSwap()
    {
        var world = new World();
        var entity = new Entity("npc", "Goblin");
        entity.AddTag("npc");
        world.TrackEntity(entity);
        world.SwapTagBuffers();
        world.GetEntitiesByTag("npc").Should().Contain(entity);

        entity.RemoveTag("npc");
        world.GetEntitiesByTag("npc").Should().Contain(entity); // still in read snapshot

        world.SwapTagBuffers();
        world.GetEntitiesByTag("npc").Should().BeEmpty();
    }

    [Fact]
    public void UntrackEntity_RemovesFromIndex_AfterSwap()
    {
        var world = new World();
        var entity = new Entity("npc", "Goblin");
        entity.AddTag("npc");
        world.TrackEntity(entity);
        world.SwapTagBuffers();

        world.UntrackEntity(entity);
        world.SwapTagBuffers();

        world.GetEntitiesByTag("npc").Should().BeEmpty();
    }

    [Fact]
    public void CoW_MutatingOneTag_DoesNotAffectOtherTag()
    {
        var world = new World();
        var npc = new Entity("npc", "Goblin");
        npc.AddTag("npc");
        var player = new Entity("player", "Rand");
        player.AddTag("player");
        world.TrackEntity(npc);
        world.TrackEntity(player);
        world.SwapTagBuffers();

        npc.RemoveTag("npc");
        world.SwapTagBuffers();

        world.GetEntitiesByTag("npc").Should().BeEmpty();
        world.GetEntitiesByTag("player").Should().Contain(player);
    }

    [Fact]
    public void GetEntitiesByTag_ReturnsIReadOnlySet()
    {
        var world = new World();
        var result = world.GetEntitiesByTag("any");
        result.Should().BeAssignableTo<IReadOnlySet<Entity>>();
    }

    [Fact]
    public void RemoveTag_OnDirtyPrunedTag_DoesNotThrow_AndIndexConsistent()
    {
        // Regression: RemoveFromWriteIndex must tolerate the "dirty-but-pruned" state
        // (tag in _dirtyTags, key absent from _writeIndex) the same way AddToWriteIndex
        // does. Manufacture that state, then drive a further tag removal for "x".
        //
        // Production manifestation: link-dead reconnect calls entity.RemoveTag("linkdead")
        // (or UntrackEntity on a disconnecting session) for a tag already pruned-dirty this
        // tick -> KeyNotFoundException -> reconnect aborts -> player bounced back to link-dead
        // -> cleanup timer reset -> ghost session never expires.
        var world = new World();
        var a = new Entity("npc", "A");
        a.AddTag("x");
        world.TrackEntity(a);
        world.SwapTagBuffers();   // read "x"->{a}, write clone {a}, dirty cleared
        world.GetEntitiesByTag("x").Should().Contain(a);

        // Within one tick (no swap): empty the write set for "x" so its key is pruned
        // out of _writeIndex while "x" stays in _dirtyTags.
        a.RemoveTag("x");         // dirty clone {a} -> remove a -> {} -> key pruned, "x" dirty

        // A further "x" removal now reaches RemoveFromWriteIndex with the key absent.
        // b holds "x" but is not part of the index set, mirroring an entity whose tag
        // was already pruned this tick (e.g. a disconnecting session being untracked).
        var b = new Entity("npc", "B");
        b.AddTag("x");
        Action act = () => world.UntrackEntity(b);

        act.Should().NotThrow<KeyNotFoundException>();

        // Index stays consistent: "x" has no live holders after the swap.
        world.SwapTagBuffers();
        world.GetEntitiesByTag("x").Should().BeEmpty();
    }

    [Fact]
    public void CoW_UndirtiedTag_SharesSetReference_AcrossSwaps()
    {
        var world = new World();
        var npc = new Entity("npc", "Goblin");
        npc.AddTag("npc");
        var player = new Entity("player", "Rand");
        player.AddTag("player");
        world.TrackEntity(npc);
        world.TrackEntity(player);
        world.SwapTagBuffers();

        var playerSetBefore = world.GetEntitiesByTag("player");

        // Mutate only the "npc" tag -- "player" should not be cloned
        npc.RemoveTag("npc");
        world.SwapTagBuffers();

        var playerSetAfter = world.GetEntitiesByTag("player");
        ReferenceEquals(playerSetBefore, playerSetAfter).Should().BeTrue(
            "CoW should not clone sets for tags that were not mutated");
    }
}
