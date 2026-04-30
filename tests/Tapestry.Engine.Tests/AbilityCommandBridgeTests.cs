using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class AbilityCommandBridgeTests
{
    private (AbilityCommandBridge bridge, CommandRegistry commands, AbilityRegistry abilities, ProficiencyManager proficiency, World world) Build()
    {
        var world = new World();
        var abilities = new AbilityRegistry();
        var proficiency = new ProficiencyManager(world, abilities);
        var commands = new CommandRegistry();
        var sessions = new SessionManager();
        var eventBus = new EventBus();
        var combat = new CombatManager(world, eventBus);

        var bridge = new AbilityCommandBridge(abilities, proficiency, commands, world, combat, sessions);
        return (bridge, commands, abilities, proficiency, world);
    }

    [Fact]
    public void WireAll_RegistersCommandForEachActiveAbility()
    {
        var (bridge, commands, abilities, _, _) = Build();
        abilities.Register(new AbilityDefinition { Id = "kick", Name = "Kick", Type = AbilityType.Active, Category = AbilityCategory.Skill });
        abilities.Register(new AbilityDefinition { Id = "bash", Name = "Bash", Type = AbilityType.Active, Category = AbilityCategory.Skill });

        bridge.WireAll();

        Assert.NotNull(commands.Resolve("kick"));
        Assert.NotNull(commands.Resolve("bash"));
    }

    [Fact]
    public void WireAll_SkipsPassiveAbilities()
    {
        var (bridge, commands, abilities, _, _) = Build();
        abilities.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge", Type = AbilityType.Passive, Category = AbilityCategory.Skill });

        bridge.WireAll();

        Assert.Null(commands.Resolve("dodge"));
    }

    [Fact]
    public void WireAll_GeneratedCommand_Category_IsSkillsForSkill()
    {
        var (bridge, commands, abilities, _, _) = Build();
        abilities.Register(new AbilityDefinition { Id = "kick", Name = "Kick", Type = AbilityType.Active, Category = AbilityCategory.Skill });

        bridge.WireAll();

        var reg = commands.Resolve("kick");
        Assert.Equal("skills", reg!.Category);
    }

    [Fact]
    public void WireAll_GeneratedCommand_Category_IsSpellsForSpell()
    {
        var (bridge, commands, abilities, _, _) = Build();
        abilities.Register(new AbilityDefinition { Id = "fireball", Name = "Fireball", Type = AbilityType.Active, Category = AbilityCategory.Spell });

        bridge.WireAll();

        var reg = commands.Resolve("fireball");
        Assert.Equal("spells", reg!.Category);
    }

    [Fact]
    public void WireAll_GeneratedCommand_VisibleTo_ReturnsTrueWhenProficiencyAboveZero()
    {
        var (bridge, commands, abilities, proficiency, world) = Build();
        abilities.Register(new AbilityDefinition { Id = "kick", Name = "Kick", Type = AbilityType.Active, Category = AbilityCategory.Skill });

        bridge.WireAll();

        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var reg = commands.Resolve("kick");

        Assert.False(reg!.VisibleTo!(player));

        proficiency.Learn(player.Id, "kick", 1);
        Assert.True(reg.VisibleTo!(player));
    }

    [Fact]
    public void WireAll_GeneratedCommand_VisibleTo_ReturnsFalseAfterForget()
    {
        var (bridge, commands, abilities, proficiency, world) = Build();
        abilities.Register(new AbilityDefinition { Id = "kick", Name = "Kick", Type = AbilityType.Active, Category = AbilityCategory.Skill });

        bridge.WireAll();

        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        proficiency.Learn(player.Id, "kick", 1);

        var reg = commands.Resolve("kick");
        Assert.True(reg!.VisibleTo!(player));

        proficiency.Forget(player.Id, "kick");
        Assert.False(reg.VisibleTo!(player));
    }

    [Fact]
    public void WireAll_GeneratedCommand_Description_UsesShortNameWhenPresent()
    {
        var (bridge, commands, abilities, _, _) = Build();
        abilities.Register(new AbilityDefinition
        {
            Id = "heron_wading_in_the_rushes",
            Name = "Heron Wading in the Rushes",
            ShortName = "Heron",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill
        });

        bridge.WireAll();

        var reg = commands.Resolve("heron_wading_in_the_rushes");
        Assert.Equal("Heron", reg!.Description);
    }

    [Fact]
    public void WireAll_GeneratedCommand_Description_FallsBackToNameWhenNoShortName()
    {
        var (bridge, commands, abilities, _, _) = Build();
        abilities.Register(new AbilityDefinition { Id = "bash", Name = "Bash", Type = AbilityType.Active, Category = AbilityCategory.Skill });

        bridge.WireAll();

        var reg = commands.Resolve("bash");
        Assert.Equal("Bash", reg!.Description);
    }

    [Fact]
    public void ResolveTarget_NamedTargetMiss_InCombat_FallsBackToPrimaryTarget()
    {
        var world = new World();
        var abilities = new AbilityRegistry();
        var proficiency = new ProficiencyManager(world, abilities);
        var commands = new CommandRegistry();
        var sessions = new SessionManager();
        var eventBus = new EventBus();
        var combat = new CombatManager(world, eventBus);

        abilities.Register(new AbilityDefinition
        {
            Id = "kick",
            Name = "Kick",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill
        });

        var bridge = new AbilityCommandBridge(abilities, proficiency, commands, world, combat, sessions);
        bridge.WireAll();

        // Set up a room and place player + mob in it
        var room = new Room("room:1", "Test Room", "A plain room.");
        world.AddRoom(room);

        var player = new Entity("player", "Tester");
        player.LocationRoomId = room.Id;
        world.TrackEntity(player);
        room.AddEntity(player);

        var mob = new Entity("npc", "Orc");
        mob.AddTag("killable");
        mob.LocationRoomId = room.Id;
        world.TrackEntity(mob);
        room.AddEntity(mob);

        proficiency.Learn(player.Id, "kick", 1);

        // Engage combat so player has a primary target
        combat.Engage(player, mob);
        Assert.True(combat.IsInCombat(player.Id));
        Assert.Equal(mob.Id, combat.GetPrimaryTarget(player.Id));

        // Invoke kick with a target name that doesn't match anyone in the room
        var ctx = new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "kick goblin",
            Command = "kick",
            Args = ["goblin"]
        };

        var reg = commands.Resolve("kick");
        Assert.NotNull(reg);
        reg!.Handler(ctx);

        // Verify an action was queued targeting the mob (combat fallback)
        var queue = player.GetProperty<List<object>>(AbilityProperties.QueuedActions);
        Assert.NotNull(queue);
        Assert.Single(queue);
        var action = (Dictionary<string, object?>)queue![0];
        Assert.Equal(mob.Id.ToString(), action["targetEntityId"]?.ToString());
    }
}
