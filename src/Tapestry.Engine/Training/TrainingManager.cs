using Tapestry.Engine.Abilities;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Training;

public class TrainingManager
{
    private readonly World _world;
    private readonly ProficiencyManager _proficiency;
    private readonly RaceRegistry _races;
    private readonly TrainingConfig _config;
    private readonly AbilityRegistry _abilities;

    public TrainingManager(World world, ProficiencyManager proficiency,
        RaceRegistry races, TrainingConfig config, AbilityRegistry abilities)
    {
        _world = world;
        _proficiency = proficiency;
        _races = races;
        _config = config;
        _abilities = abilities;
    }

    private string AbilityDisplayName(string abilityId)
    {
        var def = _abilities.Get(abilityId);
        if (def != null) { return def.ShortName ?? def.Name; }
        var colonIdx = abilityId.LastIndexOf(':');
        var shortId = colonIdx >= 0 ? abilityId[(colonIdx + 1)..] : abilityId;
        return shortId.Replace('_', ' ');
    }

    public int GetTrainsAvailable(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return 0; }
        var val = entity.GetProperty<int?>(TrainingProperties.TrainsAvailable);
        return val ?? 0;
    }

    public void GrantTrains(Guid entityId, int amount)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var current = GetTrainsAvailable(entityId);
        entity.SetProperty(TrainingProperties.TrainsAvailable, current + amount);
    }

    public TrainerMatch? FindTrainerInRoom(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity?.LocationRoomId == null) { return null; }

        foreach (var roomEntity in _world.GetEntitiesInRoom(entity.LocationRoomId))
        {
            if (!roomEntity.HasTag("skill_trainer")) { continue; }
            var cfg = roomEntity.GetProperty<TrainerConfig>(TrainingProperties.TrainerConfigKey);
            if (cfg == null) { continue; }
            return new TrainerMatch(roomEntity.Id, roomEntity.Name, cfg.Tier, cfg.AbilityIds);
        }
        return null;
    }

    public PracticeResult TryPractice(Guid entityId, string abilityId)
    {
        if (!_proficiency.HasAbility(entityId, abilityId))
        {
            return new PracticeResult(PracticeResultKind.NotLearned,
                $"You have not learned {AbilityDisplayName(abilityId)}.");
        }

        var match = FindTrainerInRoom(entityId);
        if (match == null)
        {
            return new PracticeResult(PracticeResultKind.NoTrainer,
                "There is no one here to teach you.");
        }

        if (!match.AbilityIds.Contains(abilityId))
        {
            return new PracticeResult(PracticeResultKind.CannotTeach,
                $"{match.TrainerName} cannot teach you {AbilityDisplayName(abilityId)}.");
        }

        var currentCap = _proficiency.GetCap(entityId, abilityId);
        var trainerTierValue = (int)match.Tier;

        if (trainerTierValue <= currentCap)
        {
            return new PracticeResult(PracticeResultKind.AlreadyAtOrAboveTier,
                $"You have surpassed what {match.TrainerName} can teach.");
        }

        var nextTierValue = NextTierValue(currentCap);
        if (trainerTierValue != nextTierValue)
        {
            return new PracticeResult(PracticeResultKind.TierSkip,
                $"You must master the basics before {match.TrainerName} will teach you.");
        }

        _proficiency.SetCap(entityId, abilityId, trainerTierValue);

        var currentProf = _proficiency.GetProficiency(entityId, abilityId) ?? 0;
        if (currentProf < currentCap)
        {
            var boosted = Math.Min(currentCap, currentProf + _config.CatchUpBoost);
            _proficiency.SetProficiency(entityId, abilityId, boosted);
            currentProf = _proficiency.GetProficiency(entityId, abilityId) ?? boosted;
        }

        return new PracticeResult(PracticeResultKind.Success,
            $"{match.TrainerName} teaches you more of {AbilityDisplayName(abilityId)}.",
            NewCap: trainerTierValue, NewProficiency: currentProf);
    }

    public StatTrainResult TryTrain(Guid entityId, StatType stat)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return new StatTrainResult(StatTrainResultKind.NotTrainable, "You cannot train that here.");
        }

        if (_config.RequireSafeRoomForStats)
        {
            var room = entity.LocationRoomId != null ? _world.GetRoom(entity.LocationRoomId) : null;
            if (room == null || !room.HasTag("safe"))
            {
                return new StatTrainResult(StatTrainResultKind.UnsafeRoom,
                    "You cannot train your body here.");
            }
        }

        var statName = stat.ToString().ToLower();
        if (!_config.TrainableStats.Contains(statName))
        {
            return new StatTrainResult(StatTrainResultKind.NotTrainable,
                "That is not something you can train.");
        }

        if (GetTrainsAvailable(entityId) < 1)
        {
            return new StatTrainResult(StatTrainResultKind.NoTrains,
                "You have no trains available.");
        }

        var currentValue = GetStatValue(entity, stat);
        var raceId = entity.GetProperty<string>("race") ?? "";
        var raceDef = _races.Get(raceId);
        var raceCap = raceDef?.StatCaps.GetValueOrDefault(stat) ?? 25;

        if (currentValue >= raceCap)
        {
            return new StatTrainResult(StatTrainResultKind.AtRaceCap,
                $"Your {stat} cannot go any higher.");
        }

        entity.SetProperty(TrainingProperties.TrainsAvailable, GetTrainsAvailable(entityId) - 1);
        SetStatBase(entity, stat, GetStatBaseValue(entity, stat) + 1);
        entity.Stats.Invalidate();

        return new StatTrainResult(StatTrainResultKind.Success,
            $"Your {stat} increases to {GetStatValue(entity, stat)}.",
            NewValue: GetStatValue(entity, stat));
    }

    private static int NextTierValue(int currentCap) => currentCap switch
    {
        25 => 50,
        50 => 75,
        75 => 100,
        _ => 100
    };

    private static int GetStatValue(Entity entity, StatType stat) => stat switch
    {
        StatType.Strength => entity.Stats.Strength,
        StatType.Intelligence => entity.Stats.Intelligence,
        StatType.Wisdom => entity.Stats.Wisdom,
        StatType.Dexterity => entity.Stats.Dexterity,
        StatType.Constitution => entity.Stats.Constitution,
        StatType.Luck => entity.Stats.Luck,
        _ => 0
    };

    private static int GetStatBaseValue(Entity entity, StatType stat) => stat switch
    {
        StatType.Strength => entity.Stats.BaseStrength,
        StatType.Intelligence => entity.Stats.BaseIntelligence,
        StatType.Wisdom => entity.Stats.BaseWisdom,
        StatType.Dexterity => entity.Stats.BaseDexterity,
        StatType.Constitution => entity.Stats.BaseConstitution,
        StatType.Luck => entity.Stats.BaseLuck,
        _ => 0
    };

    private static void SetStatBase(Entity entity, StatType stat, int value)
    {
        switch (stat)
        {
            case StatType.Strength: entity.Stats.BaseStrength = value; break;
            case StatType.Intelligence: entity.Stats.BaseIntelligence = value; break;
            case StatType.Wisdom: entity.Stats.BaseWisdom = value; break;
            case StatType.Dexterity: entity.Stats.BaseDexterity = value; break;
            case StatType.Constitution: entity.Stats.BaseConstitution = value; break;
            case StatType.Luck: entity.Stats.BaseLuck = value; break;
        }
    }
}

public record TrainerMatch(Guid TrainerEntityId, string TrainerName, CapTier Tier,
    IReadOnlyList<string> AbilityIds);

public enum PracticeResultKind { Success, NotLearned, NoTrainer, CannotTeach, AlreadyAtOrAboveTier, TierSkip }
public record PracticeResult(PracticeResultKind Kind, string Message,
    int? NewCap = null, int? NewProficiency = null);

public enum StatTrainResultKind { Success, NoTrains, AtRaceCap, NotTrainable, UnsafeRoom }
public record StatTrainResult(StatTrainResultKind Kind, string Message, int? NewValue = null);
