using Tapestry.Engine;
using Tapestry.Engine.Stats;

namespace Tapestry.Scripting.Services;

public class ApiStats
{
    private readonly World _world;
    private readonly StatDisplayNames _statDisplayNames;
    private readonly VitalsService _vitalsService;

    public ApiStats(World world, StatDisplayNames statDisplayNames, VitalsService vitalsService)
    {
        _world = world;
        _statDisplayNames = statDisplayNames;
        _vitalsService = vitalsService;
    }

    public object? GetEntityStats(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return null;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return null;
        }

        return new
        {
            strength = entity.Stats.Strength,
            baseStrength = entity.Stats.BaseStrength,
            intelligence = entity.Stats.Intelligence,
            baseIntelligence = entity.Stats.BaseIntelligence,
            wisdom = entity.Stats.Wisdom,
            baseWisdom = entity.Stats.BaseWisdom,
            dexterity = entity.Stats.Dexterity,
            baseDexterity = entity.Stats.BaseDexterity,
            constitution = entity.Stats.Constitution,
            baseConstitution = entity.Stats.BaseConstitution,
            luck = entity.Stats.Luck,
            baseLuck = entity.Stats.BaseLuck,
            hp = entity.Stats.Hp,
            maxHp = entity.Stats.MaxHp,
            resource = entity.Stats.Resource,
            maxResource = entity.Stats.MaxResource,
            movement = entity.Stats.Movement,
            maxMovement = entity.Stats.MaxMovement
        };
    }

    public void SetEntityHp(string entityIdStr, int value)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            var entity = _world.GetEntity(entityId);
            if (entity != null)
            {
                _vitalsService.Set(entity, VitalKind.Hp, value, "script.set");
            }
        }
    }

    public void SetEntityResource(string entityIdStr, int value)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            var entity = _world.GetEntity(entityId);
            if (entity != null)
            {
                _vitalsService.Set(entity, VitalKind.Resource, value, "script.set");
            }
        }
    }

    public void SetEntityMovement(string entityIdStr, int value)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            var entity = _world.GetEntity(entityId);
            if (entity != null)
            {
                _vitalsService.Set(entity, VitalKind.Movement, value, "script.set");
            }
        }
    }

    public void AddStatModifier(string entityIdStr, string source, string statName, int value)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        if (Enum.TryParse<StatType>(statName, true, out var statType))
        {
            entity.Stats.AddModifier(new StatModifier(source, statType, value));
        }
    }

    public void RemoveStatModifiers(string entityIdStr, string source)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            var entity = _world.GetEntity(entityId);
            entity?.Stats.RemoveModifiersBySource(source);
        }
    }

    public bool AddBaseAttribute(string entityIdStr, string statName, int amount)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return false;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return false;
        }

        var lowerStat = statName.ToLowerInvariant();
        switch (lowerStat)
        {
            case "strength":
            case "str":
                entity.Stats.BaseStrength += amount;
                break;
            case "intelligence":
            case "int":
                entity.Stats.BaseIntelligence += amount;
                break;
            case "wisdom":
            case "wis":
                entity.Stats.BaseWisdom += amount;
                break;
            case "dexterity":
            case "dex":
                entity.Stats.BaseDexterity += amount;
                break;
            case "constitution":
            case "con":
                entity.Stats.BaseConstitution += amount;
                break;
            case "luck":
            case "luc":
                entity.Stats.BaseLuck += amount;
                break;
            case "maxhp":
            case "max_hp":
                entity.Stats.BaseMaxHp += amount;
                break;
            case "maxresource":
            case "max_resource":
                entity.Stats.BaseMaxResource += amount;
                break;
            case "maxmovement":
            case "max_movement":
                entity.Stats.BaseMaxMovement += amount;
                break;
            default:
                return false;
        }
        return true;
    }

    public void RestoreVitals(string entityIdStr)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            var entity = _world.GetEntity(entityId);
            if (entity != null)
            {
                _vitalsService.RestoreToMax(entity, "script.restore");
            }
        }
    }

    public void AddVital(string entityIdStr, string vital, int amount)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        switch (vital.ToLowerInvariant())
        {
            case "hp":
                _vitalsService.Apply(entity, VitalKind.Hp, amount, "script.add");
                break;
            case "mana":
            case "resource":
                _vitalsService.Apply(entity, VitalKind.Resource, amount, "script.add");
                break;
            case "mv":
            case "movement":
                _vitalsService.Apply(entity, VitalKind.Movement, amount, "script.add");
                break;
        }
    }

    public string GetStatDisplayName(string statName)
    {
        return _statDisplayNames.GetDisplayName(statName);
    }
}
