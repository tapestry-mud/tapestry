using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Alignment;

public class AbilityAlignmentGateTests
{
    [Fact]
    public void Ability_WithAlignmentRange_FizzlesWhenCasterOutOfRange()
    {
        var world = new World();
        var eventBus = new EventBus();
        var abilityRegistry = new AbilityRegistry();
        var proficiencyManager = new ProficiencyManager(world, abilityRegistry);
        var effectManager = new EffectManager(world, eventBus);
        var sessionManager = new SessionManager();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);

        var room = new Room("test-room", "Test Room", "A test room.");
        world.AddRoom(room);

        // Register ability restricted to deeply evil (max -700)
        abilityRegistry.Register(new AbilityDefinition
        {
            Id = "invoke_darkness",
            Name = "Invoke Shayol Ghul",
            AlignmentRange = new AlignmentRange { Max = -700 }
        });

        // Create player with neutral alignment (0), queue the ability
        var player = new Entity("player", "Tester");
        player.AddTag("player");
        room.AddEntity(player);
        world.TrackEntity(player);

        // Grant proficiency so the alignment gate is what fizzles (not proficiency check)
        proficiencyManager.Learn(player.Id, "invoke_darkness", 95);

        player.SetProperty("queued_actions", new List<object>
        {
            new Dictionary<string, object?> { ["abilityId"] = "invoke_darkness" }
        });

        var fizzled = new List<GameEvent>();
        eventBus.Subscribe("ability.fizzled", e => { fizzled.Add(e); });

        // Execute one pulse using the phase with alignment manager injected
        var phase = new AbilityResolutionPhase(alignmentManager: alignmentManager);
        var context = new PulseContext
        {
            CurrentTick = 1,
            CurrentPulse = 1,
            World = world,
            EventBus = eventBus,
            CombatManager = combatManager,
            AbilityRegistry = abilityRegistry,
            ProficiencyManager = proficiencyManager,
            EffectManager = effectManager,
            SessionManager = sessionManager,
            AlignmentManager = alignmentManager,
            Random = new Random(42)
        };
        phase.Execute(context);

        Assert.Single(fizzled);
        Assert.Equal("alignment_restricted", fizzled[0].Data["reason"]);
    }
}
