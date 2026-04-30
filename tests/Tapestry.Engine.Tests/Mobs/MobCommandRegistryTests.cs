using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine.Mobs;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Mobs;

public class MobCommandRegistryTests
{
    private static (MobCommandRegistry registry, World world, EventBus eventBus) BuildRegistry()
    {
        var world = new World();
        var eventBus = new EventBus();
        var registry = new MobCommandRegistry(world, eventBus, NullLogger<MobCommandRegistry>.Instance);
        return (registry, world, eventBus);
    }

    private static Entity PlaceMobInRoom(World world, string roomId)
    {
        var room = new Room(roomId, "Room", ".");
        world.AddRoom(room);
        var entity = new Entity("npc", "Guard");
        entity.LocationRoomId = roomId;
        world.TrackEntity(entity);
        return entity;
    }

    [Fact]
    public void Dispatch_CallsRegisteredHandler()
    {
        var (registry, world, _) = BuildRegistry();
        var entity = PlaceMobInRoom(world, "test:room");

        var called = false;
        var receivedText = "";
        registry.Register("say", new MobCommandRegistration
        {
            Handler = (mob, text) => { called = true; receivedText = text; }
        });

        registry.Dispatch(entity.Id, "say Hello world!");

        Assert.True(called);
        Assert.Equal("Hello world!", receivedText);
    }

    [Fact]
    public void Dispatch_PassesMobContextToHandler()
    {
        var (registry, world, _) = BuildRegistry();
        var entity = PlaceMobInRoom(world, "test:room");

        MobContext? capturedCtx = null;
        registry.Register("emote", new MobCommandRegistration
        {
            Handler = (mob, text) => { capturedCtx = mob; }
        });

        registry.Dispatch(entity.Id, "emote waves.");

        Assert.NotNull(capturedCtx);
        Assert.Equal(entity.Id, capturedCtx!.EntityId);
        Assert.Equal("Guard", capturedCtx.Name);
        Assert.Equal("test:room", capturedCtx.RoomId);
    }

    [Fact]
    public void Dispatch_PublishesCommunicationEvent_WhenGmcpChannelSet()
    {
        var (registry, world, eventBus) = BuildRegistry();
        var entity = PlaceMobInRoom(world, "test:room");

        registry.Register("say", new MobCommandRegistration
        {
            Handler = (mob, text) => { },
            GmcpChannel = "say",
            PrependSender = false
        });

        GameEvent? publishedEvent = null;
        eventBus.Subscribe("communication.message", evt => publishedEvent = evt);

        registry.Dispatch(entity.Id, "say Hello!");

        Assert.NotNull(publishedEvent);
        Assert.Equal("say", publishedEvent!.Data["channel"]);
        Assert.Equal("mob", publishedEvent.Data["source"]);
        Assert.Equal("Hello!", publishedEvent.Data["text"]);
        Assert.Equal("Guard", publishedEvent.Data["sender"]);
    }

    [Fact]
    public void Dispatch_PrependsSenderName_WhenPrependSenderTrue()
    {
        var (registry, world, eventBus) = BuildRegistry();
        var entity = PlaceMobInRoom(world, "test:room");

        registry.Register("emote", new MobCommandRegistration
        {
            Handler = (mob, text) => { },
            GmcpChannel = "emote",
            PrependSender = true
        });

        GameEvent? publishedEvent = null;
        eventBus.Subscribe("communication.message", evt => publishedEvent = evt);

        registry.Dispatch(entity.Id, "emote waves.");

        Assert.NotNull(publishedEvent);
        Assert.Equal("Guard waves.", publishedEvent!.Data["text"]);
    }

    [Fact]
    public void Dispatch_DoesNotPublishEvent_WhenNoGmcpChannel()
    {
        var (registry, world, eventBus) = BuildRegistry();
        var entity = PlaceMobInRoom(world, "test:room");

        registry.Register("open", new MobCommandRegistration
        {
            Handler = (mob, text) => { }
        });

        var eventFired = false;
        eventBus.Subscribe("communication.message", _ => eventFired = true);

        registry.Dispatch(entity.Id, "open door");

        Assert.False(eventFired);
    }

    [Fact]
    public void Dispatch_SilentlyIgnoresUnknownVerb()
    {
        var (registry, world, _) = BuildRegistry();
        var entity = PlaceMobInRoom(world, "test:room");

        var ex = Record.Exception(() => registry.Dispatch(entity.Id, "unknownverb foo"));
        Assert.Null(ex);
    }

    [Fact]
    public void Dispatch_SilentlyIgnoresNullEntity()
    {
        var (registry, world, _) = BuildRegistry();

        registry.Register("say", new MobCommandRegistration
        {
            Handler = (mob, text) => { }
        });

        var ex = Record.Exception(() => registry.Dispatch(Guid.NewGuid(), "say Hello!"));
        Assert.Null(ex);
    }
}
