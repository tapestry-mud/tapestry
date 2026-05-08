using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests.Mobs;

public class MobCommandQueueTests
{
    private static (World world, Entity entity, CommandRouter router, CommandRegistry commandRegistry) BuildWorld()
    {
        var world = new World();
        var room = new Room("test:room", "Room", ".");
        world.AddRoom(room);
        var entity = new Entity("npc", "Guard");
        entity.LocationRoomId = "test:room";
        world.TrackEntity(entity);

        var commandRegistry = new CommandRegistry();
        var sessions = new SessionManager();
        var router = new CommandRouter(commandRegistry, sessions, world);
        return (world, entity, router, commandRegistry);
    }

    [Fact]
    public void ProcessTick_DispatchesImmediateCommand()
    {
        var (world, entity, router, commandRegistry) = BuildWorld();
        var dispatched = new List<string>();
        commandRegistry.Register(
            "say",
            handler: ctx => { dispatched.Add(string.Join(" ", ctx.Args)); },
            roles: new[] { "mob" });

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, router, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Hello!", 0);
        queue.ProcessTick();

        Assert.Single(dispatched);
        Assert.Equal("Hello!", dispatched[0]);
    }

    [Fact]
    public void ProcessTick_DoesNotDispatchFutureCommand()
    {
        var (world, entity, router, commandRegistry) = BuildWorld();
        commandRegistry.Register(
            "say",
            handler: ctx => { },
            roles: new[] { "mob" });

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, router, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Hello!", 2);
        queue.ProcessTick();

        Assert.Single(queue.GetScheduledTicks(entity.Id));
    }

    [Fact]
    public void ProcessTick_DispatchesAfterDelayElapses()
    {
        var (world, entity, router, commandRegistry) = BuildWorld();
        var dispatched = new List<string>();
        commandRegistry.Register(
            "say",
            handler: ctx => { dispatched.Add(string.Join(" ", ctx.Args)); },
            roles: new[] { "mob" });

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, router, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Delayed!", 2);

        for (var i = 0; i < 20; i++)
        {
            timer.Advance();
        }
        queue.ProcessTick();

        Assert.Single(dispatched);
        Assert.Equal("Delayed!", dispatched[0]);
    }

    [Fact]
    public void Enqueue_ChainsRelativeToLastScheduledCommand()
    {
        var (world, entity, router, _) = BuildWorld();

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, router, timer, NullLogger<MobCommandQueue>.Instance);

        queue.Enqueue(entity.Id, "say A", 0);
        queue.Enqueue(entity.Id, "say B", 2);
        queue.Enqueue(entity.Id, "say C", 2);

        var ticks = queue.GetScheduledTicks(entity.Id);
        Assert.Equal(3, ticks.Count);
        Assert.Equal(0L, ticks[0]);
        Assert.Equal(20L, ticks[1]);
        Assert.Equal(40L, ticks[2]);
    }

    [Fact]
    public void ProcessTick_SilentlyDropsCommandForRemovedEntity()
    {
        var (world, entity, router, _) = BuildWorld();

        var timer = new TickTimer(10);
        var queue = new MobCommandQueue(world, router, timer, NullLogger<MobCommandQueue>.Instance);
        queue.Enqueue(entity.Id, "say Hello!", 0);

        world.UntrackEntity(entity);

        var ex = Record.Exception((Action)(() => queue.ProcessTick()));
        Assert.Null(ex);
    }
}
