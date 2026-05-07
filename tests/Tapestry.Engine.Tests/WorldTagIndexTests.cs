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
