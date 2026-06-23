using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;

namespace Tapestry.Scripting.Tests.Modules;

public class CombatModuleTests
{
    private (JintRuntime rt, World world, CombatManager combat) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<CombatManager>());
    }

    private (JintRuntime rt, RegistrationPolicy policy, WindowValidatorRegistry registry, PackDependencyGraph graph)
        BuildRuntimeWithRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<PackDependencyGraph>();
        graph.Build(new Dictionary<string, List<string>>());
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt,
            provider.GetRequiredService<RegistrationPolicy>(),
            provider.GetRequiredService<WindowValidatorRegistry>(),
            graph);
    }

    private (Entity entityA, Entity entityB) CreateCombatPair(World world, CombatManager combat)
    {
        var entityA = new Entity("npc", "goblin");
        var entityB = new Entity("npc", "orc");
        world.TrackEntity(entityA);
        world.TrackEntity(entityB);
        combat.Engage(entityA, entityB);
        return (entityA, entityB);
    }

    [Fact]
    public void RemoveFromAllCombat_ClearsBothSides()
    {
        var (rt, world, combat) = BuildRuntime();
        var (entityA, entityB) = CreateCombatPair(world, combat);

        Assert.True(combat.IsInCombat(entityA.Id));
        Assert.True(combat.IsInCombat(entityB.Id));

        EsmTest.Load(rt, "test-pack", $"tapestry.combat.removeFromAllCombat('{entityA.Id}')");

        Assert.False(combat.IsInCombat(entityA.Id));
        Assert.False(combat.IsInCombat(entityB.Id));
    }

    [Fact]
    public void RemoveFromAllCombat_MultiOpponent_ClearsAllThreeSides()
    {
        // A is engaged with BOTH B and C (two symmetric pairs: A-B, A-C).
        // B and C each have only A as their opponent.
        // Removing A from all combat must clear A, B, and C -- the foreach
        // over A's opponent list is the path under test.
        var (rt, world, combat) = BuildRuntime();
        var entityA = new Entity("npc", "goblin");
        var entityB = new Entity("npc", "orc");
        var entityC = new Entity("npc", "troll");
        world.TrackEntity(entityA);
        world.TrackEntity(entityB);
        world.TrackEntity(entityC);
        combat.Engage(entityA, entityB);
        combat.Engage(entityA, entityC);

        Assert.True(combat.IsInCombat(entityA.Id));
        Assert.True(combat.IsInCombat(entityB.Id));
        Assert.True(combat.IsInCombat(entityC.Id));

        EsmTest.Load(rt, "test-pack", $"tapestry.combat.removeFromAllCombat('{entityA.Id}')");

        Assert.False(combat.IsInCombat(entityA.Id));
        Assert.False(combat.IsInCombat(entityB.Id));
        Assert.False(combat.IsInCombat(entityC.Id));
    }

    [Fact]
    public void RemoveFromAllCombat_UnknownId_DoesNotThrow()
    {
        var (rt, _, _) = BuildRuntime();
        var unknownId = Guid.NewGuid().ToString();
        var ex = Record.Exception(() => EsmTest.Load(rt, "test-pack", $"tapestry.combat.removeFromAllCombat('{unknownId}')"));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveFromAllCombat_GarbageId_DoesNotThrow()
    {
        var (rt, _, _) = BuildRuntime();
        var ex = Record.Exception(() => EsmTest.Load(rt, "test-pack", "tapestry.combat.removeFromAllCombat('not-a-guid')"));
        Assert.Null(ex);
    }

    [Fact]
    public void RegisterWindow_RegistersValidator_ThroughTheSeal()
    {
        var (rt, policy, registry, _) = BuildRuntimeWithRegistry();

        EsmTest.Load(rt, "test-pack",
            """
            tapestry.combat.registerWindow('telegraph-rung', function (ctx) {
              if (!ctx.command.verb) { return { outcome: 'WEATHERED', narrationKey: 'weathered' }; }
              return ctx.command.verb === ctx.swell.requiredCounter
                ? { outcome: 'COUNTERED', narrationKey: 'countered' }
                : { outcome: 'WHIFFED', narrationKey: 'whiffed' };
            });
            """);

        policy.Resolve();

        var validator = registry.Get("telegraph-rung");
        Assert.NotNull(validator);

        var ctx = new CombatContext
        {
            Actor = new ActorView(Guid.NewGuid(), "perfect"),
            Target = new ActorView(Guid.NewGuid(), "wounded"),
            Phase = "swell",
            Swell = new SwellView("sweep", "sidestep", "full", true),
            Command = new CommandView("sidestep", null)
        };
        Assert.Equal(WindowOutcome.Countered, validator!(ctx).Outcome);

        // A WRONG counter (the bug-2 report: "wrong counter still damages the boss").
        // The real Jint validator must return WHIFFED, not COUNTERED, so resolve damages
        // the player and never the boss. Proves the resolve path is not the source of boss damage.
        var wrongCtx = new CombatContext
        {
            Actor = new ActorView(Guid.NewGuid(), "perfect"),
            Target = new ActorView(Guid.NewGuid(), "wounded"),
            Phase = "swell",
            Swell = new SwellView("sweep", "sidestep", "full", true),
            Command = new CommandView("brace", null)
        };
        Assert.Equal(WindowOutcome.Whiffed, validator!(wrongCtx).Outcome);
    }
}
