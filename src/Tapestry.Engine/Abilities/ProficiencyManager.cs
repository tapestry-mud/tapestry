using Tapestry.Engine.Quests;

namespace Tapestry.Engine.Abilities;

public record LearnedAbility(string AbilityId, int Proficiency);

public class ProficiencyManager : IQuestProficiencyService
{
    private readonly World _world;
    private readonly AbilityRegistry _registry;

    public ProficiencyManager(World world, AbilityRegistry registry)
    {
        _world = world;
        _registry = registry;
    }

    public void Learn(Guid entityId, string abilityId, int initialProficiency = 1)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }
        var clamped = Math.Clamp(initialProficiency, 1, 100);
        var profMap = GetOrCreateMap(entity, AbilityProperties.Proficiency);
        profMap[abilityId] = clamped;
        entity.SetProperty(AbilityProperties.Proficiency, profMap);

        var capMap = GetOrCreateMap(entity, AbilityProperties.Cap);
        if (!capMap.ContainsKey(abilityId))
        {
            capMap[abilityId] = 25;
            entity.SetProperty(AbilityProperties.Cap, capMap);
        }
    }

    public int GetCap(Guid entityId, string abilityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return 100; }
        var capMap = entity.GetProperty<Dictionary<string, int>>(AbilityProperties.Cap);
        if (capMap == null || !capMap.TryGetValue(abilityId, out var capValue)) { return 100; }
        return capValue;
    }

    public void SetCap(Guid entityId, string abilityId, int capValue)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var capMap = GetOrCreateMap(entity, AbilityProperties.Cap);
        capMap[abilityId] = capValue;
        entity.SetProperty(AbilityProperties.Cap, capMap);
    }

    public void Forget(Guid entityId, string abilityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }
        var profMap = entity.GetProperty<Dictionary<string, int>>(AbilityProperties.Proficiency);
        if (profMap != null)
        {
            profMap.Remove(abilityId);
            entity.SetProperty(AbilityProperties.Proficiency, profMap.Count > 0 ? profMap : null);
        }
    }

    public int? GetProficiency(Guid entityId, string abilityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return null;
        }
        var profMap = entity.GetProperty<Dictionary<string, int>>(AbilityProperties.Proficiency);
        if (profMap == null || !profMap.TryGetValue(abilityId, out var profValue))
        {
            return null;
        }
        return profValue;
    }

    public bool HasAbility(Guid entityId, string abilityId)
    {
        return GetProficiency(entityId, abilityId) != null;
    }

    public void SetProficiency(Guid entityId, string abilityId, int value)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null || !HasAbility(entityId, abilityId))
        {
            return;
        }
        var clamped = Math.Clamp(value, 1, 100);
        var profMap = GetOrCreateMap(entity, AbilityProperties.Proficiency);
        profMap[abilityId] = clamped;
        entity.SetProperty(AbilityProperties.Proficiency, profMap);
    }

    public void IncreaseProficiency(Guid entityId, string abilityId, int amount, int cap = 100)
    {
        var current = GetProficiency(entityId, abilityId);
        if (current == null)
        {
            return;
        }
        var maxValue = Math.Min(cap, 100);
        var newValue = Math.Min(current.Value + amount, maxValue);
        SetProficiency(entityId, abilityId, newValue);
    }

    public void RollProficiencyGain(Guid entityId, string abilityId, Random random, bool wasFailure = false)
    {
        var current = GetProficiency(entityId, abilityId);
        if (current == null) { return; }

        var cap = GetCap(entityId, abilityId);
        if (current.Value >= cap) { return; }

        var definition = _registry.Get(abilityId);
        if (definition == null) { return; }

        var entity = _world.GetEntity(entityId);
        var gainStatMultiplier = 1.0;
        if (entity != null && !string.IsNullOrEmpty(definition.GainStat))
        {
            var statValue = GetStatByName(entity, definition.GainStat);
            gainStatMultiplier = 1.0 + (statValue * definition.GainStatScale);
        }

        var effectiveChance = definition.ProficiencyGainChance * (1.0 - current.Value / 100.0) * gainStatMultiplier;
        if (wasFailure)
        {
            effectiveChance *= definition.FailureProficiencyGainMultiplier;
        }

        if (random.NextDouble() < effectiveChance)
        {
            SetProficiency(entityId, abilityId, current.Value + 1);
        }
    }

    public List<LearnedAbility> GetLearnedAbilities(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return new List<LearnedAbility>();
        }
        var profMap = entity.GetProperty<Dictionary<string, int>>(AbilityProperties.Proficiency);
        if (profMap == null)
        {
            return new List<LearnedAbility>();
        }
        return profMap.Select(kvp => new LearnedAbility(kvp.Key, kvp.Value)).ToList();
    }

    private static Dictionary<string, int> GetOrCreateMap(Entity entity, string propertyKey)
    {
        return entity.GetProperty<Dictionary<string, int>>(propertyKey) ?? new Dictionary<string, int>();
    }

    private static int GetStatByName(Entity entity, string statName)
    {
        return statName.ToLowerInvariant() switch
        {
            "strength" => entity.Stats.Strength,
            "intelligence" => entity.Stats.Intelligence,
            "wisdom" => entity.Stats.Wisdom,
            "dexterity" => entity.Stats.Dexterity,
            "constitution" => entity.Stats.Constitution,
            "luck" => entity.Stats.Luck,
            _ => 0
        };
    }
}
