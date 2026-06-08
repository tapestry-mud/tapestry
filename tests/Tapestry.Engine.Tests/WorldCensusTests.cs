using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class WorldCensusTests
{
    [Fact]
    public void SampleCensus_CountsEntitiesByType_TagsAndProperties()
    {
        var world = new World();

        var room = new Room("a:room", "Room", "A room.");
        world.AddRoom(room);

        var npc = new Entity("npc", "Goblin");
        npc.SetProperty("hp", 10);
        npc.SetProperty("behavior", "wander");   // 2 properties
        world.TrackEntity(npc);
        // Tags added AFTER TrackEntity so the world tag-observer indexes them.
        npc.AddTag("hostile");
        npc.AddTag("goblinoid");

        var item = new Entity("item", "Sword");
        item.SetProperty("template_id", "sword"); // 1 property
        world.TrackEntity(item);
        item.AddTag("hostile");                   // shared tag key

        // Tags land in the write index; SwapTagBuffers promotes them to the read
        // index that queries (and SampleCensus) see — pre_tick does this each tick.
        world.SwapTagBuffers();

        var census = world.SampleCensus();

        census.EntitiesByType["npc"].Should().Be(1);
        census.EntitiesByType["item"].Should().Be(1);
        census.PropertiesTotal.Should().Be(3);        // 2 + 1
        census.MaxEntityProperties.Should().Be(2);     // the npc
        census.TagCount.Should().Be(2);                // "hostile", "goblinoid"
        census.TagMemberships.Should().Be(3);          // hostile×2 + goblinoid×1
    }

    [Fact]
    public void SampleCensus_AfterRemoval_DoesNotCountGoneEntities()
    {
        var world = new World();
        var npc = new Entity("npc", "Goblin");
        world.TrackEntity(npc);
        world.SampleCensus().EntitiesByType.GetValueOrDefault("npc").Should().Be(1);

        world.UntrackEntity(npc);

        world.SampleCensus().EntitiesByType.GetValueOrDefault("npc").Should().Be(0);
    }
}
