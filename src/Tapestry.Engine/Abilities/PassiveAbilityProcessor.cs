namespace Tapestry.Engine.Abilities;

public class PassiveAbilityProcessor
{
    private readonly AbilityRegistry _registry;
    private readonly ProficiencyManager _proficiency;

    public PassiveAbilityProcessor(AbilityRegistry registry, ProficiencyManager proficiency)
    {
        _registry = registry;
        _proficiency = proficiency;
    }

    public bool CheckBinaryPassive(Guid entityId, string abilityId, Random random)
    {
        var prof = _proficiency.GetProficiency(entityId, abilityId);
        if (prof == null)
        {
            return false;
        }

        var definition = _registry.Get(abilityId);
        var maxChance = definition?.MaxChance ?? 100;
        var effectiveChance = (int)(prof.Value * (maxChance / 100.0));
        var roll = random.Next(1, 101);
        return roll <= effectiveChance;
    }

    public int GetScalingBonus(Guid entityId, string abilityId)
    {
        var prof = _proficiency.GetProficiency(entityId, abilityId);
        if (prof == null)
        {
            return 0;
        }
        var definition = _registry.Get(abilityId);
        if (definition == null)
        {
            return 0;
        }
        var maxBonus = 0;
        if (definition.Metadata.TryGetValue("max_bonus", out var maxBonusObj) && maxBonusObj != null)
        {
            maxBonus = Convert.ToInt32(maxBonusObj);
        }
        return (int)(maxBonus * (prof.Value / 100.0));
    }

    public int GetExtraAttackCount(Guid entityId, Random random)
    {
        var extraAttacks = 0;
        var attackPassives = GetPassivesByHook("extra_attack");
        foreach (var passive in attackPassives)
        {
            var prof = _proficiency.GetProficiency(entityId, passive.Id);
            if (prof == null)
            {
                continue;
            }

            var effectiveChance = (int)(prof.Value * (passive.MaxChance / 100.0));
            var roll = random.Next(1, 101);

            if (roll <= effectiveChance)
            {
                extraAttacks++;
                _proficiency.RollProficiencyGain(entityId, passive.Id, random);
            }
        }
        return extraAttacks;
    }

    public string? CheckDefensivePassives(Guid entityId, Random random)
    {
        var defensivePassives = GetPassivesByHook("defensive_check");

        foreach (var passive in defensivePassives)
        {
            var prof = _proficiency.GetProficiency(entityId, passive.Id);
            if (prof == null)
            {
                continue;
            }

            var effectiveChance = (int)(prof.Value * (passive.MaxChance / 100.0));
            var roll = random.Next(1, 101);

            if (roll <= effectiveChance)
            {
                _proficiency.RollProficiencyGain(entityId, passive.Id, random);
                return passive.Id;
            }
        }

        return null;
    }

    private IEnumerable<AbilityDefinition> GetPassivesByHook(string hook)
    {
        return _registry.GetByType(AbilityType.Passive)
            .Where(a =>
            {
                if (a.Metadata.TryGetValue("hook", out var hookObj))
                {
                    return hookObj?.ToString() == hook;
                }
                return false;
            });
    }
}
