namespace Tapestry.Engine.Abilities;

public record LearnedAbility(string AbilityId, int Proficiency);

public class ProficiencyManager
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
        entity.SetProperty(AbilityProperties.Proficiency(abilityId), clamped);

        var capKey = AbilityProperties.Cap(abilityId);
        if (!entity.GetAllProperties().ContainsKey(capKey))
        {
            entity.SetProperty(capKey, 25);
        }
    }

    public int GetCap(Guid entityId, string abilityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return 100; }
        var allProps = entity.GetAllProperties();
        var capKey = AbilityProperties.Cap(abilityId);
        if (!allProps.TryGetValue(capKey, out var raw) || raw == null) { return 100; }
        return (int)raw;
    }

    public void SetCap(Guid entityId, string abilityId, int capValue)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        entity.SetProperty(AbilityProperties.Cap(abilityId), capValue);
    }

    public void Forget(Guid entityId, string abilityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }
        entity.SetProperty(AbilityProperties.Proficiency(abilityId), null);
    }

    public int? GetProficiency(Guid entityId, string abilityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return null;
        }
        var key = AbilityProperties.Proficiency(abilityId);
        var allProps = entity.GetAllProperties();
        if (!allProps.TryGetValue(key, out var raw) || raw == null)
        {
            return null;
        }
        return (int)raw;
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
        entity.SetProperty(AbilityProperties.Proficiency(abilityId), clamped);
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

        var effectiveChance = definition.ProficiencyGainChance * (1.0 - current.Value / 100.0);
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
        var result = new List<LearnedAbility>();
        foreach (var prop in entity.GetAllProperties())
        {
            if (prop.Key.StartsWith(AbilityProperties.ProficiencyPrefix) && prop.Value is int proficiency)
            {
                var abilityId = prop.Key[AbilityProperties.ProficiencyPrefix.Length..];
                result.Add(new LearnedAbility(abilityId, proficiency));
            }
        }
        return result;
    }
}
