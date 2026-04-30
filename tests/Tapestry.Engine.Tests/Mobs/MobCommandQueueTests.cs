using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests.Mobs;

public class MobCommandQueueTests
{
    private static (World world, Entity entity, MobCommandRegistry registry) BuildWorld()
    {
        var world = new World();
        var room = new Room("test:room", "Room", ".");
        world.AddRoom(room);
        var entity = new Entity("npc", "Guard");
        entity.LocationRoomId = "test:room";
        world.TrackEntity(entity);

        var eventBus = new EventBus();
        var registry = new MobCommandRegistry(world, eventBus, NullLogger<MobCommandRegistry>.Instance);
        return (world, entity, registry);
    }

    [Fact]
    public void ProcessTick_DispatchesImmediateCommand()
    {
        var (world, entity, registry) = BuildWorld();
        var dispatched = new List<string>();
        registry.Register("say", new MobCommandRegistration { Handler = (mob, text) => { dispatched.Add(text); } });

        var timer = new TickTimer(10); // 10 ticks/second; starts at tick 0
        var queue = new MobCommandQueue(world, registry, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Hello!", 0); // schedules at tick 0
        queue.ProcessTick();                        // current tick is 0 -- fires immediately

        Assert.Single(dispatched);
        Assert.Equal("Hello!", dispatched[0]);
    }

    [Fact]
    public void ProcessTick_DoesNotDispatchFutureCommand()
    {
        var (world, entity, registry) = BuildWorld();
        registry.Register("say", new MobCommandRegistration { Handler = (mob, text) => { } });

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, registry, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Hello!", 2); // schedules at tick 0 + 20 = tick 20
        queue.ProcessTick();                        // current tick is 0 -- should NOT fire

        Assert.Equal(1, queue.GetScheduledTicks(entity.Id).Count);
    }

    [Fact]
    public void ProcessTick_DispatchesAfterDelayElapses()
    {
        var (world, entity, registry) = BuildWorld();
        var dispatched = new List<string>();
        registry.Register("say", new MobCommandRegistration { Handler = (mob, text) => { dispatched.Add(text); } });

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, registry, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Delayed!", 2); // fires at tick 20

        for (var i = 0; i < 20; i++) { timer.Advance(); }
        queue.ProcessTick(); // current tick is 20 -- fires now

        Assert.Single(dispatched);
        Assert.Equal("Delayed!", dispatched[0]);
    }

    [Fact]
    public void Enqueue_ChainsRelativeToLastScheduledCommand()
    {
        var (world, entity, registry) = BuildWorld();
        registry.Register("say", new MobCommandRegistration { Handler = (mob, text) => { } });

        var timer = new TickTimer(10); // 10 tps; starts at tick 0
        var queue = new MobCommandQueue(world, registry, timer, NullLogger<MobCommandQueue>.Instance);

        queue.Enqueue(entity.Id, "say A", 0); // fires at tick 0
        queue.Enqueue(entity.Id, "say B", 2); // fires at tick 0 + 20 = 20
        queue.Enqueue(entity.Id, "say C", 2); // fires at tick 20 + 20 = 40

        var ticks = queue.GetScheduledTicks(entity.Id);
        Assert.Equal(3, ticks.Count);
        Assert.Equal(0L,  ticks[0]);
        Assert.Equal(20L, ticks[1]);
        Assert.Equal(40L, ticks[2]);
    }

    [Fact]
    public void ProcessTick_SilentlyDropsCommandForRemovedEntity()
    {
        var (world, entity, registry) = BuildWorld();
        registry.Register("say", new MobCommandRegistration { Handler = (mob, text) => { } });

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, registry, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Hello!", 0);

        world.UntrackEntity(entity);

        var ex = Record.Exception((Action)(() => queue.ProcessTick()));
        Assert.Null(ex);
    }
}
