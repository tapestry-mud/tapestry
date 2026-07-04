using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Heartbeat;

public class SwellClockPulseTests
{
    [Fact]
    public void HasCadence1AndPriority90()
    {
        var world = new World();
        var eventBus = new EventBus();
        var combat = new CombatManager(world, eventBus);
        var validators = new WindowValidatorRegistry();
        var manager = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));

        var pulse = new SwellClockPulse(manager);

        Assert.Equal(1, pulse.Cadence);
        Assert.Equal(90, pulse.Priority);
    }

    [Fact]
    public void Execute_AdvancesTheClock()
    {
        var world = new World();
        var eventBus = new EventBus();
        var combat = new CombatManager(world, eventBus);
        var validators = new WindowValidatorRegistry();
        var manager = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var pulse = new SwellClockPulse(manager);

        var room = new Room("core:arena", "Arena", "A test arena.");
        world.AddRoom(room);

        var player = new Entity("player", "Travis");
        player.Stats.BaseMaxHp = 100;
        player.Stats.Hp = 100;
        room.AddEntity(player);
        world.TrackEntity(player);

        var boss = new Entity("npc", "the swell-warden");
        boss.Stats.BaseMaxHp = 200;
        boss.Stats.Hp = 200;
        boss.SetProperty("swell_window", "telegraph-rung");
        boss.SetProperty("swell_baseline_gap_ticks", 1);
        boss.SetProperty("swell_jitter_ticks", 0);
        boss.SetProperty("swell_telegraph_ticks", 1);
        boss.SetProperty("swell_window_ticks", 1);
        boss.SetProperty("swell_tell", "full");
        boss.SetProperty("swell_line1_id", "sweep");
        boss.SetProperty("swell_line1_counter", "sidestep");
        boss.SetProperty("swell_line1_tell_full", "SWEEP -- sidestep!");
        boss.SetProperty("swell_line2_id", "crush");
        boss.SetProperty("swell_line2_counter", "brace");
        boss.SetProperty("swell_line2_tell_full", "CRUSH -- brace!");
        room.AddEntity(boss);
        world.TrackEntity(boss);
        combat.Engage(player, boss);

        var telegraphs = new List<GameEvent>();
        eventBus.Subscribe("combat.swell.telegraph", e => telegraphs.Add(e));

        pulse.Execute(new PulseContext { CurrentTick = 0, World = world, EventBus = eventBus, CombatManager = combat });
        pulse.Execute(new PulseContext { CurrentTick = 1, World = world, EventBus = eventBus, CombatManager = combat });

        Assert.NotEmpty(telegraphs);
    }
}
