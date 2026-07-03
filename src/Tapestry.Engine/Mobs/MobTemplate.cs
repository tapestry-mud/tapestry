using Tapestry.Engine;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Training;

namespace Tapestry.Engine.Mobs;

public class MobAbilityEntry
{
    public string Id { get; set; } = "";
    public int? Proficiency { get; set; }
}

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
    public List<string> Keywords { get; set; } = new();
    public Disposition BaseDisposition { get; set; } = Tapestry.Engine.Disposition.Neutral;
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
    public List<MobAbilityEntry> Abilities { get; set; } = new();
    public List<string> BattleCommands { get; set; } = new();
    public double? BattleChance { get; set; }
    public int? BattleInterval { get; set; }
    public int? AbilityProficiency { get; set; }
    public TrainerConfig? TrainerConfig { get; set; }
    public ShopConfig? ShopConfig { get; set; }
    public List<string> PatrolRoute { get; set; } = new();
    public List<string> ShopSells { get; set; } = new();

    public Entity CreateEntity()
    {
        var entity = new Entity(Type, Name);

        foreach (var tag in Tags)
        {
            if (!string.Equals(tag, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                entity.AddTag(tag);
            }
        }

        foreach (var keyword in Keywords)
        {
            entity.AddKeyword(keyword);
        }

        entity.Disposition = BaseDisposition;

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
            if (kvp.Key == MobProperties.MobLevel)
            {
                entity.SetProperty("level", new Dictionary<string, int> { ["combat"] = Convert.ToInt32(kvp.Value) });
                continue;
            }
            entity.SetProperty(kvp.Key, kvp.Value);
        }

        entity.SetProperty(CommonProperties.TemplateId, Id);
        entity.SetProperty(MobProperties.Behavior, Behavior);

        if (PatrolRoute.Count > 0)
        {
            entity.SetProperty("patrol_route", PatrolRoute);
        }
        if (ShopSells.Count > 0)
        {
            entity.SetProperty("shop_sells", ShopSells);
        }
        if (IdleCommands.Count > 0)
        {
            entity.SetProperty(MobProperties.IdleCommands, IdleCommands);
            entity.SetProperty(MobProperties.IdleChance, IdleChance);
            entity.SetProperty(MobProperties.IdleInterval, IdleInterval);
        }
        if (BattleCommands.Count > 0)
        {
            entity.SetProperty(MobProperties.BattleCommands, BattleCommands);
            if (BattleChance.HasValue) { entity.SetProperty(MobProperties.BattleChance, BattleChance.Value); }
            if (BattleInterval.HasValue) { entity.SetProperty(MobProperties.BattleInterval, BattleInterval.Value); }
        }

        if (Disposition != null)
        {
            entity.DispositionRules = new DispositionDefinition
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
        }

        if (!string.IsNullOrEmpty(Script))
        {
            entity.SetProperty("script", Script);
        }

        if (TrainerConfig != null)
        {
            entity.TrainerConfig = TrainerConfig;
        }

        if (ShopConfig != null)
        {
            entity.ShopConfig = ShopConfig;
        }

        return entity;
    }
}
