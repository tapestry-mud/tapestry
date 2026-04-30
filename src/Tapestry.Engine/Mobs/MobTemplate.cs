namespace Tapestry.Engine.Mobs;

public class DispositionConditionModel
{
    public int? MinAlignment { get; set; }
    public int? MaxAlignment { get; set; }
    public List<string>? Buckets { get; set; }
    public string? HasTag { get; set; }
}

public class DispositionRuleModel
{
    public DispositionConditionModel When { get; set; } = new();
    public string Reaction { get; set; } = "neutral";
}

public class DispositionModel
{
    public string Default { get; set; } = "neutral";
    public List<DispositionRuleModel> Rules { get; set; } = new();
}

public class MobTemplateStats
{
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Luck { get; set; }
    public int MaxHp { get; set; }
    public int MaxResource { get; set; }
    public int MaxMovement { get; set; }
}

public class MobTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "npc";
    public List<string> Tags { get; set; } = new();
    public string Behavior { get; set; } = "stationary";
    public MobTemplateStats Stats { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
    public List<string> Equipment { get; set; } = new();
    public string? LootTable { get; set; }
    public string? Class { get; set; }
    public string? Race { get; set; }
    public int Level { get; set; }
    public DispositionModel? Disposition { get; set; }
    public List<string> IdleCommands { get; set; } = new();
    public double IdleChance { get; set; } = 0.3;
    public int IdleInterval { get; set; } = 30;
    public string? Script { get; set; }

    public Entity CreateEntity()
    {
        var entity = new Entity(Type, Name);

        foreach (var tag in Tags)
        {
            entity.AddTag(tag);
        }

        entity.Stats.BaseStrength = Stats.Strength;
        entity.Stats.BaseIntelligence = Stats.Intelligence;
        entity.Stats.BaseWisdom = Stats.Wisdom;
        entity.Stats.BaseDexterity = Stats.Dexterity;
        entity.Stats.BaseConstitution = Stats.Constitution;
        entity.Stats.BaseLuck = Stats.Luck;
        entity.Stats.BaseMaxHp = Stats.MaxHp;
        entity.Stats.BaseMaxResource = Stats.MaxResource;
        entity.Stats.BaseMaxMovement = Stats.MaxMovement;
        entity.Stats.Hp = Stats.MaxHp;
        entity.Stats.Resource = Stats.MaxResource;
        entity.Stats.Movement = Stats.MaxMovement;

        foreach (var kvp in Properties)
        {
            entity.SetProperty(kvp.Key, kvp.Value);
        }

        entity.SetProperty(CommonProperties.TemplateId, Id);
        entity.SetProperty(MobProperties.Behavior, Behavior);

        if (Disposition != null)
        {
            var disposition = new DispositionDefinition
            {
                Default = Disposition.Default,
                Rules = Disposition.Rules.Select(r => new DispositionRule
                {
                    Reaction = r.Reaction,
                    When = new DispositionCondition
                    {
                        MinAlignment = r.When.MinAlignment,
                        MaxAlignment = r.When.MaxAlignment,
                        Buckets = r.When.Buckets,
                        HasTag = r.When.HasTag
                    }
                }).ToList()
            };
            entity.SetProperty("disposition", disposition);
        }

        if (IdleCommands.Count > 0)
        {
            entity.SetProperty("idle_commands", IdleCommands);
            entity.SetProperty("idle_chance", IdleChance);
            entity.SetProperty("idle_interval", IdleInterval);
        }

        if (!string.IsNullOrEmpty(Script))
        {
            entity.SetProperty("script", Script);
        }

        return entity;
    }
}
