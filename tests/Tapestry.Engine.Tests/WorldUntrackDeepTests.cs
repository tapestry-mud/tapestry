using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

/// <summary>
/// Verifies that UntrackEntityDeep removes the holder AND every carried/equipped/nested
/// item from the tag index, preventing the leak where plain UntrackEntity left orphaned
/// memberships for items the holder was carrying.
/// </summary>
public class WorldUntrackDeepTests
{
    // -------------------------------------------------------------------------
    // Helper: build and track an entity with a specific tag.
    // -------------------------------------------------------------------------
    private static Entity MakeTracked(World world, string type, string name, string tag)
    {
        var entity = new Entity(type, name);
        entity.AddTag(tag);
        world.TrackEntity(entity);
        return entity;
    }

    // -------------------------------------------------------------------------
    // Test 1 (the fix): UntrackEntityDeep removes holder + all carried items.
    // Covers: item in Contents, item in Equipment, nested container + its item.
    // -------------------------------------------------------------------------
    [Fact]
    public void UntrackEntityDeep_RemovesHolder_And_AllCarriedItems_FromTagIndex()
    {
        var world = new World();

        // Holder (a player-like entity)
        var holder = MakeTracked(world, "player", "Rand", "player");

        // A simple item in Contents
        var sword = MakeTracked(world, "item", "Sword", "weapon");
        holder.AddToContents(sword);

        // An equipped item (lives in Equipment, not Contents)
        var helmet = MakeTracked(world, "item", "Helmet", "armor");
        holder.SetEquipment("head", helmet);

        // A container in Contents that itself holds a nested item
        var bag = MakeTracked(world, "item", "Bag", "container");
        holder.AddToContents(bag);
        var gem = MakeTracked(world, "item", "Gem", "gem");
        bag.AddToContents(gem);

        // Promote all write-index mutations into the read index so assertions are visible.
        world.SwapTagBuffers();

        // Baseline: each tag has its entity in the read index.
        world.GetEntitiesByTag("player").Should().Contain(holder, "holder should be tracked");
        world.GetEntitiesByTag("weapon").Should().Contain(sword, "sword should be tracked");
        world.GetEntitiesByTag("armor").Should().Contain(helmet, "helmet should be tracked");
        world.GetEntitiesByTag("container").Should().Contain(bag, "bag should be tracked");
        world.GetEntitiesByTag("gem").Should().Contain(gem, "gem should be tracked");

        // Act: deep-untrack the holder.
        world.UntrackEntityDeep(holder);
        world.SwapTagBuffers();

        // Assert: nothing remains in the tag index.
        world.GetEntitiesByTag("player").Should().BeEmpty("holder must be removed from tag index");
        world.GetEntitiesByTag("weapon").Should().BeEmpty("Contents item must be removed from tag index");
        world.GetEntitiesByTag("armor").Should().BeEmpty("Equipment item must be removed from tag index");
        world.GetEntitiesByTag("container").Should().BeEmpty("Contents container must be removed from tag index");
        world.GetEntitiesByTag("gem").Should().BeEmpty("Nested item inside container must be removed from tag index");

        // Assert: none are still in the entity dictionary.
        world.GetEntity(holder.Id).Should().BeNull("holder must be untracked from entity dict");
        world.GetEntity(sword.Id).Should().BeNull("sword must be untracked from entity dict");
        world.GetEntity(helmet.Id).Should().BeNull("helmet must be untracked from entity dict");
        world.GetEntity(bag.Id).Should().BeNull("bag must be untracked from entity dict");
        world.GetEntity(gem.Id).Should().BeNull("gem must be untracked from entity dict");
    }

    // -------------------------------------------------------------------------
    // Test 2 (bug documentation): plain UntrackEntity leaves carried items
    // orphaned in the tag index, demonstrating why the deep variant is needed.
    // -------------------------------------------------------------------------
    [Fact]
    public void UntrackEntity_Shallow_LeavesCarriedItems_InTagIndex()
    {
        var world = new World();

        var holder = MakeTracked(world, "player", "Rand", "player");

        var sword = MakeTracked(world, "item", "Sword", "weapon");
        holder.AddToContents(sword);

        var helmet = MakeTracked(world, "item", "Helmet", "armor");
        holder.SetEquipment("head", helmet);

        world.SwapTagBuffers();

        // Baseline: everything is in the index.
        world.GetEntitiesByTag("weapon").Should().Contain(sword);
        world.GetEntitiesByTag("armor").Should().Contain(helmet);

        // Act: shallow untrack only.
        world.UntrackEntity(holder);
        world.SwapTagBuffers();

        // Holder is gone from its own tag.
        world.GetEntitiesByTag("player").Should().BeEmpty("holder was untracked");

        // BUG DOCUMENTED: carried items are still pinned in the tag index.
        world.GetEntitiesByTag("weapon").Should().Contain(sword,
            "shallow UntrackEntity leaves Contents items orphaned in tag index");
        world.GetEntitiesByTag("armor").Should().Contain(helmet,
            "shallow UntrackEntity leaves Equipment items orphaned in tag index");
    }
}
