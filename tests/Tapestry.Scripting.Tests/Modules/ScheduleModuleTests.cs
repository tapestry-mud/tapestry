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

    [Fact]
    public void SurvivalDrain_IntegrationTest_SustenanceDropsAfterDrainCadence()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var loop = provider.GetRequiredService<GameLoop>();
        var world = provider.GetRequiredService<World>();
        rt.Initialize();

        var entity = new Entity("player", "TestPlayer");
        world.TrackEntity(entity);
        entity.SetProperty("sustenance", 100);

        rt.Execute("""
            var DRAIN_AMOUNT = 1;
            var DRAIN_CADENCE = 300;
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
            tapestry.schedule.everyForEach(DRAIN_CADENCE, { type: 'player' }, function(entity) {
                var current = getSustenanceValue(entity.id);
                tapestry.world.setProperty(entity.id, 'sustenance', Math.max(0, current - DRAIN_AMOUNT));
            });
        """, "@tapestry/survival");

        for (var i = 0; i < 300; i++) { loop.Tick(); }

        var raw = world.GetEntity(entity.Id)!.GetProperty<object>("sustenance");
        var sustenance = Convert.ToInt32(raw);
        sustenance.Should().Be(99);
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

    [Fact]
    public void SurvivalItemConsumed_AppliesSustenanceValue_CappedAt100()
    {
        // The survival item.consumed subscriber now owns nutrition application
        // (moved out of the engine's ConsumableService). Verifies the apply path and
        // the 100 cap in the real Jint runtime. Kept in parity with
        // @tapestry/survival/scripts/sustenance.js.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var bus = provider.GetRequiredService<EventBus>();
        var world = provider.GetRequiredService<World>();
        rt.Initialize();

        var hungry = new Entity("player", "Hungry");
        world.TrackEntity(hungry);
        hungry.SetProperty("sustenance", 50);

        var nearFull = new Entity("player", "NearFull");
        world.TrackEntity(nearFull);
        nearFull.SetProperty("sustenance", 90);

        rt.Execute("""
            function getSustenanceValue(entityId) {
                var val = tapestry.world.getProperty(entityId, 'sustenance');
                return (val === null || val === undefined) ? 100 : val;
            }
            tapestry.events.on('item.consumed', function(evt) {
                var entityId = evt.data.entityId;
                var sustenanceValue = Number(evt.data.sustenanceValue) || 0;
                if (sustenanceValue > 0) {
                    var current = Number(getSustenanceValue(entityId)) || 0;
                    tapestry.world.setProperty(entityId, 'sustenance', Math.min(100, current + sustenanceValue));
                }
            });
        """, "@tapestry/survival");

        bus.Publish(new GameEvent
        {
            Type = "item.consumed",
            SourceEntityId = hungry.Id,
            Data = new Dictionary<string, object?> { ["entityId"] = hungry.Id.ToString(), ["sustenanceValue"] = 30 }
        });
        bus.Publish(new GameEvent
        {
            Type = "item.consumed",
            SourceEntityId = nearFull.Id,
            Data = new Dictionary<string, object?> { ["entityId"] = nearFull.Id.ToString(), ["sustenanceValue"] = 30 }
        });

        // Read value-tolerantly: JS writes numbers as double, so verify the nutrition
        // LOGIC (apply + cap) by value. The double-vs-int storage issue is a separate
        // engine concern — see the GMCP hunger read finding.
        Convert.ToInt32(world.GetEntity(hungry.Id)!.GetProperty<object>("sustenance")).Should().Be(80);
        Convert.ToInt32(world.GetEntity(nearFull.Id)!.GetProperty<object>("sustenance")).Should().Be(100); // capped
    }

    [Fact]
    public void SurvivalSeedsSustenance_OnCharacterCreated_OnlyIfUnset()
    {
        // Seeding moved out of the engine (WorldEventModule) into survival. Must seed a
        // fresh character to 100 but never clobber an existing value (e.g. a famished 0/20).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var bus = provider.GetRequiredService<EventBus>();
        var world = provider.GetRequiredService<World>();
        rt.Initialize();

        var fresh = new Entity("player", "Fresh");
        world.TrackEntity(fresh);                  // no sustenance set
        var existing = new Entity("player", "Existing");
        world.TrackEntity(existing);
        existing.SetProperty("sustenance", 20);    // already partway down — must be preserved

        rt.Execute("""
            function seedSustenance(entityId) {
                if (!entityId) { return; }
                var raw = tapestry.world.getProperty(entityId, 'sustenance');
                if (raw === null || raw === undefined) {
                    tapestry.world.setProperty(entityId, 'sustenance', 100);
                }
            }
            tapestry.events.on('character.created', function(evt) { seedSustenance(evt.sourceEntityId); });
        """, "@tapestry/survival");

        bus.Publish(new GameEvent { Type = "character.created", SourceEntityId = fresh.Id });
        bus.Publish(new GameEvent { Type = "character.created", SourceEntityId = existing.Id });

        (world.GetEntity(fresh.Id)!.TryGetProperty<int>("sustenance", out var f) ? f : -1).Should().Be(100);
        (world.GetEntity(existing.Id)!.TryGetProperty<int>("sustenance", out var e) ? e : -1).Should().Be(20);
    }
}
