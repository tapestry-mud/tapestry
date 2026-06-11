using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Scripting;

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

        rt.Execute($"tapestry.combat.removeFromAllCombat('{entityA.Id}')");

        Assert.False(combat.IsInCombat(entityA.Id));
        Assert.False(combat.IsInCombat(entityB.Id));
    }

    [Fact]
    public void RemoveFromAllCombat_UnknownId_DoesNotThrow()
    {
        var (rt, _, _) = BuildRuntime();
        var unknownId = Guid.NewGuid().ToString();
        var ex = Record.Exception(() => rt.Execute($"tapestry.combat.removeFromAllCombat('{unknownId}')"));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveFromAllCombat_GarbageId_DoesNotThrow()
    {
        var (rt, _, _) = BuildRuntime();
        var ex = Record.Exception(() => rt.Execute("tapestry.combat.removeFromAllCombat('not-a-guid')"));
        Assert.Null(ex);
    }
}
