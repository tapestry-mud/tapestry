using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Classes;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Classes;

public class ClassPathProcessorTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void LevelUp_GrantsPathEntry_WhenTrackAndLevelMatch()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        entity.SetProperty("class", "warrior");

        classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Track = "combat",
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "dodge", null) }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?> { ["track"] = "combat", ["oldLevel"] = 0, ["newLevel"] = 1, ["entityId"] = entity.Id.ToString() }
        });

        Assert.True(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void LevelUp_SkipsEntry_WhenUnlockedViaIsSet()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "Tester2");
        world.TrackEntity(entity);
        entity.SetProperty("class", "warrior");

        classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Track = "combat",
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "dodge", "quest") }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?> { ["track"] = "combat", ["oldLevel"] = 0, ["newLevel"] = 1, ["entityId"] = entity.Id.ToString() }
        });

        Assert.False(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void LevelUp_NoGrant_WhenTrackDoesNotMatch()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "Tester3");
        world.TrackEntity(entity);
        entity.SetProperty("class", "warrior");

        classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Track = "combat",
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "dodge", null) }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?> { ["track"] = "magic", ["oldLevel"] = 0, ["newLevel"] = 1, ["entityId"] = entity.Id.ToString() }
        });

        Assert.False(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void LevelUp_UnknownAbility_SkipsAndContinuesToNext()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "Tester4");
        world.TrackEntity(entity);
        entity.SetProperty("class", "warrior");

        classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Track = "combat",
            Path = new List<ClassPathEntry>
            {
                new ClassPathEntry(1, "unknown_ability", null),
                new ClassPathEntry(1, "dodge", null)
            }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?> { ["track"] = "combat", ["oldLevel"] = 0, ["newLevel"] = 1, ["entityId"] = entity.Id.ToString() }
        });

        Assert.True(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void LevelUp_NoGrant_WhenEntityHasNoClass()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "Tester5");
        world.TrackEntity(entity);

        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?> { ["track"] = "combat", ["oldLevel"] = 0, ["newLevel"] = 1, ["entityId"] = entity.Id.ToString() }
        });

        Assert.False(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void CharacterCreated_GrantsLevel1PathEntries()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "TesterCreated1");
        world.TrackEntity(entity);
        entity.SetProperty("class", "warrior");

        classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Track = "combat",
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "dodge", null) }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "character.created",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?>()
        });

        Assert.True(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void CharacterCreated_SkipsEntry_WhenUnlockedViaIsSet()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "TesterCreated2");
        world.TrackEntity(entity);
        entity.SetProperty("class", "warrior");

        classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Track = "combat",
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "dodge", "quest") }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge" });

        eventBus.Publish(new GameEvent
        {
            Type = "character.created",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?>()
        });

        Assert.False(proficiency.HasAbility(entity.Id, "dodge"));
    }

    [Fact]
    public void CharacterCreated_GrantsLevel1Entry_AnyTrack()
    {
        var provider = BuildProvider();
        var world = provider.GetRequiredService<World>();
        var classRegistry = provider.GetRequiredService<ClassRegistry>();
        var abilityRegistry = provider.GetRequiredService<AbilityRegistry>();
        var proficiency = provider.GetRequiredService<ProficiencyManager>();
        var eventBus = provider.GetRequiredService<EventBus>();
        provider.GetRequiredService<ClassPathProcessor>();

        var entity = new Entity("player", "TesterCreated3");
        world.TrackEntity(entity);
        entity.SetProperty("class", "mage");

        classRegistry.Register(new ClassDefinition
        {
            Id = "mage",
            Name = "Mage",
            Track = "magic",
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "cure_light", null) }
        });
        abilityRegistry.Register(new AbilityDefinition { Id = "cure_light", Name = "Cure Light" });

        eventBus.Publish(new GameEvent
        {
            Type = "character.created",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?>()
        });

        Assert.True(proficiency.HasAbility(entity.Id, "cure_light"));
    }
}
