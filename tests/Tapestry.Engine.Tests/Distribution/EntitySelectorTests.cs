using Tapestry.Engine.Distribution;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests.Distribution;

public class EntitySelectorTests
{
    // --- MatchesEntity ---
    [Fact]
    public void MatchesEntity_ByType_ReturnsTrue()
    {
        var entity = new Entity("npc", "Goblin");
        var spec = new SelectorSpec(Type: "npc");
        Assert.True(EntitySelector.MatchesEntity(entity, spec));
    }

    [Fact]
    public void MatchesEntity_ByType_WrongType_ReturnsFalse()
    {
        var entity = new Entity("item", "Stone");
        var spec = new SelectorSpec(Type: "npc");
        Assert.False(EntitySelector.MatchesEntity(entity, spec));
    }

    [Fact]
    public void MatchesEntity_ByTag_ReturnsTrue()
    {
        var entity = new Entity("npc", "Cave Bat");
        entity.AddTag("cave-dweller");
        var spec = new SelectorSpec(Tag: "cave-dweller");
        Assert.True(EntitySelector.MatchesEntity(entity, spec));
    }

    [Fact]
    public void MatchesEntity_ByTag_TagAbsent_ReturnsFalse()
    {
        var entity = new Entity("npc", "Cave Bat");
        var spec = new SelectorSpec(Tag: "cave-dweller");
        Assert.False(EntitySelector.MatchesEntity(entity, spec));
    }

    [Fact]
    public void MatchesEntity_ById_TemplateId_ReturnsTrue()
    {
        var entity = new Entity("npc", "Bat");
        entity.SetProperty(CommonProperties.TemplateId, "tapestry-core:bat");
        var spec = new SelectorSpec(Id: "tapestry-core:bat");
        Assert.True(EntitySelector.MatchesEntity(entity, spec));
    }

    [Fact]
    public void MatchesEntity_Shop_ReturnsFalse()
    {
        var entity = new Entity("npc", "Merchant");
        var spec = new SelectorSpec(Shop: true);
        Assert.False(EntitySelector.MatchesEntity(entity, spec));
    }

    // --- MatchesRoom ---
    [Fact]
    public void MatchesRoom_ByTag_ReturnsTrue()
    {
        var room = new Room("test:forest", "Forest", "Trees.");
        room.AddTag("forest-room");
        var spec = new SelectorSpec(Tag: "forest-room");
        Assert.True(EntitySelector.MatchesRoom(room, spec));
    }

    [Fact]
    public void MatchesRoom_ByTag_TagAbsent_ReturnsFalse()
    {
        var room = new Room("test:forest", "Forest", "Trees.");
        var spec = new SelectorSpec(Tag: "forest-room");
        Assert.False(EntitySelector.MatchesRoom(room, spec));
    }

    [Fact]
    public void MatchesRoom_ById_ReturnsTrue()
    {
        var room = new Room("tapestry-core:mine-shaft", "Mine Shaft", "Dark.");
        var spec = new SelectorSpec(Id: "tapestry-core:mine-shaft");
        Assert.True(EntitySelector.MatchesRoom(room, spec));
    }

    [Fact]
    public void MatchesRoom_ByTypeRoom_ReturnsTrue()
    {
        var room = new Room("test:any", "Any Room", ".");
        var spec = new SelectorSpec(Type: "room");
        Assert.True(EntitySelector.MatchesRoom(room, spec));
    }

    [Fact]
    public void MatchesRoom_Shop_ReturnsFalse()
    {
        var room = new Room("test:shop", "Shop", ".");
        var spec = new SelectorSpec(Shop: true);
        Assert.False(EntitySelector.MatchesRoom(room, spec));
    }

    // --- ResolveEntities ---
    [Fact]
    public void ResolveEntities_ByType_ReturnsMatchingEntities()
    {
        var world = new World();
        var room = new Room("r1", "Room", ".");
        world.AddRoom(room);

        var e1 = new Entity("npc", "Bat");
        var e2 = new Entity("item", "Stone");
        world.TrackEntity(e1);
        world.TrackEntity(e2);

        var spec = new SelectorSpec(Type: "npc");
        var results = EntitySelector.ResolveEntities(world, spec).ToList();

        Assert.Single(results);
        Assert.Equal(e1.Id, results[0].Id);
    }

    [Fact]
    public void ResolveEntities_Shop_ReturnsEmpty()
    {
        var world = new World();
        var spec = new SelectorSpec(Shop: true);
        Assert.Empty(EntitySelector.ResolveEntities(world, spec));
    }

    [Fact]
    public void ResolveEntities_ByTemplateId_ReturnsMatchingEntities()
    {
        var world = new World();
        var e1 = new Entity("npc", "Bat");
        e1.SetProperty(CommonProperties.TemplateId, "core:bat");
        var e2 = new Entity("npc", "Goblin");
        e2.SetProperty(CommonProperties.TemplateId, "core:goblin");
        world.TrackEntity(e1);
        world.TrackEntity(e2);

        var spec = new SelectorSpec(Id: "core:bat");
        var results = EntitySelector.ResolveEntities(world, spec).ToList();

        Assert.Single(results);
        Assert.Equal(e1.Id, results[0].Id);
    }
}
