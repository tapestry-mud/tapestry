using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharItemsHandlerTests
{
    private record Harness(
        CharItemsHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        SessionManager Sessions,
        EventBus EventBus,
        Entity Player,
        string ConnectionId);

    private static Harness Build()
    {
        var cm = new FakeGmcpConnectionManager();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var slots = new SlotRegistry();
        var rarity = new RarityRegistry();
        var essence = new EssenceRegistry();

        var handler = new CharItemsHandler(cm, sessions, world, eb, slots, rarity, essence);

        var entity = new Entity("player", "Hero");
        world.TrackEntity(entity);
        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, sessions, eb, entity, conn.Id);
    }

    [Fact]
    public void SendBurst_SendsCharItemsAndCharEquipment()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Items");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Equipment");
    }

    [Fact]
    public void ItemPickedUpEvent_SendsCharItems()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.item.picked_up",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Items");
    }

    [Fact]
    public void EntityEquippedEvent_SendsBothItemsAndEquipment()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.equipped",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Items");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Equipment");
    }

    [Fact]
    public void PackageNames_ContainsBothPackages()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Items").And.Contain("Char.Equipment");
    }
}
