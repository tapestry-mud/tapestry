using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class ScheduleModuleTests
{
    [Fact]
    public void Every_RegistersTickHandler_ThatFiresAtInterval()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var loop = provider.GetRequiredService<GameLoop>();
        var bus = provider.GetRequiredService<EventBus>();
        rt.Initialize();

        var fireCount = 0;
        bus.Subscribe("test.fired", _ => fireCount++);

        rt.Execute("""
            tapestry.schedule.every(3, function() {
                tapestry.events.publish('test.fired', {});
            });
        """, "@tapestry/test");

        loop.Tick(); // 1
        loop.Tick(); // 2
        loop.Tick(); // 3 — fires
        fireCount.Should().Be(1);

        loop.Tick(); // 4
        loop.Tick(); // 5
        loop.Tick(); // 6 — fires again
        fireCount.Should().Be(2);
    }

    [Fact]
    public void Every_ReturnedHandle_CanBeCancelledBeforeFiring()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var loop = provider.GetRequiredService<GameLoop>();
        var bus = provider.GetRequiredService<EventBus>();
        rt.Initialize();

        var fireCount = 0;
        bus.Subscribe("must.not.fire", _ => fireCount++);

        rt.Execute("""
            var handle = tapestry.schedule.every(1, function() {
                tapestry.events.publish('must.not.fire', {});
            });
            tapestry.schedule.cancel(handle);
        """, "@tapestry/test");

        loop.Tick();
        fireCount.Should().Be(0);
    }

    [Fact]
    public void EveryForEach_FiresCallbackOncePerMatchingEntity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var world = provider.GetRequiredService<World>();
        var loop = provider.GetRequiredService<GameLoop>();
        var bus = provider.GetRequiredService<EventBus>();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();

        var p1 = new Entity("player", "Player1");
        var p2 = new Entity("player", "Player2");
        world.TrackEntity(p1);
        world.TrackEntity(p2);

        var calledIds = new List<string>();
        bus.Subscribe("sched.test.entity", evt =>
        {
            calledIds.Add(evt.Data["entityId"]?.ToString() ?? "");
        });

        rt.Execute("""
            tapestry.schedule.everyForEach(1, { type: 'player' }, function(entity) {
                tapestry.events.publish('sched.test.entity', { entityId: entity.id });
            });
        """, "@tapestry/test");

        loop.Tick();

        calledIds.Should().HaveCount(2);
        calledIds.Should().Contain(p1.Id.ToString());
        calledIds.Should().Contain(p2.Id.ToString());
    }

    [Fact]
    public void ReloadSafe_ReRegisteringPackHandlers_Replaces()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var loop = provider.GetRequiredService<GameLoop>();
        var bus = provider.GetRequiredService<EventBus>();
        var scheduleMod = provider.GetRequiredService<ScheduleModule>();
        rt.Initialize();

        var fireCount = 0;
        bus.Subscribe("reload.test", _ => fireCount++);

        // First load
        rt.Execute("""
            tapestry.schedule.every(1, function() {
                tapestry.events.publish('reload.test', {});
            });
        """, "@tapestry/survival");

        // Simulate reload: reset pack, re-execute
        scheduleMod.ResetPack("@tapestry/survival");
        rt.Execute("""
            tapestry.schedule.every(1, function() {
                tapestry.events.publish('reload.test', {});
            });
        """, "@tapestry/survival");

        loop.Tick();

        fireCount.Should().Be(1); // replaced, not stacked
    }

    private static string RegenScalingScript => """
        var TIER_FULL_MIN = 67;
        var TIER_HUNGRY_MIN = 34;
        function getSustenanceValue(entityId) {
            var val = tapestry.world.getProperty(entityId, 'sustenance');
            return (val === null || val === undefined) ? 100 : val;
        }
        function getTier(value) {
            if (value >= TIER_FULL_MIN) { return 'full'; }
            if (value >= TIER_HUNGRY_MIN) { return 'hungry'; }
            return 'famished';
        }
        tapestry.events.on('entity.regen', function(evt) {
            var tier = getTier(getSustenanceValue(evt.sourceEntityId));
            var mult = tier === 'full' ? 1.0 : tier === 'hungry' ? 0.5 : 0.0;
            evt.data.amount = Math.round(evt.data.amount * mult);
            if (mult === 0.0) { evt.cancel(); }
        });
    """;

    [Fact]
    public void SurvivalRegenSubscriber_CancelsRegen_WhenFamished()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var bus = provider.GetRequiredService<EventBus>();
        var world = provider.GetRequiredService<World>();
        rt.Initialize();

        var entity = new Entity("player", "Test");
        world.TrackEntity(entity);
        entity.SetProperty("sustenance", 0); // famished

        rt.Execute(RegenScalingScript, "@tapestry/survival");

        var regenData = new Dictionary<string, object?> { ["vital"] = "hp", ["amount"] = 10 };
        var regenEvent = new GameEvent { Type = "entity.regen", SourceEntityId = entity.Id, Data = regenData };
        bus.Publish(regenEvent);

        regenEvent.Cancelled.Should().BeTrue();
    }

    [Fact]
    public void SurvivalRegenSubscriber_HalvesAmount_WhenHungry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var bus = provider.GetRequiredService<EventBus>();
        var world = provider.GetRequiredService<World>();
        rt.Initialize();

        var entity = new Entity("player", "Test");
        world.TrackEntity(entity);
        entity.SetProperty("sustenance", 50); // hungry (34–66)

        rt.Execute(RegenScalingScript, "@tapestry/survival");

        var regenData = new Dictionary<string, object?> { ["vital"] = "hp", ["amount"] = 10 };
        var regenEvent = new GameEvent { Type = "entity.regen", SourceEntityId = entity.Id, Data = regenData };
        bus.Publish(regenEvent);

        regenEvent.Cancelled.Should().BeFalse();
        Convert.ToInt32(regenData["amount"]).Should().Be(5);
    }
}
