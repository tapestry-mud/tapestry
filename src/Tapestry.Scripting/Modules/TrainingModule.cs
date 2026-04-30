using Tapestry.Engine.Abilities;
using Tapestry.Engine.Training;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class TrainingModule : IJintApiModule
{
    private readonly TrainingManager _training;
    private readonly ProficiencyManager _proficiency;
    private readonly TrainingConfig _config;

    public TrainingModule(TrainingManager training, ProficiencyManager proficiency, TrainingConfig config)
    {
        _training = training;
        _proficiency = proficiency;
        _config = config;
    }

    public string Namespace => "training";

    public object Build(JintEngine engine)
    {
        return new
        {
            getTrainsAvailable = new Func<string, int>(entityIdStr =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return 0; }
                return _training.GetTrainsAvailable(id);
            }),

            grantTrains = new Action<string, int>((entityIdStr, amount) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                _training.GrantTrains(id, amount);
            }),

            getCap = new Func<string, string, string>((entityIdStr, abilityId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return "novice"; }
                var capValue = _proficiency.GetCap(id, abilityId);
                return capValue switch
                {
                    25 => "novice",
                    50 => "apprentice",
                    75 => "journeyman",
                    _ => "master"
                };
            }),

            setCap = new Action<string, string, string>((entityIdStr, abilityId, tierStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                var capValue = tierStr.ToLower() switch
                {
                    "novice" => 25,
                    "apprentice" => 50,
                    "journeyman" => 75,
                    "master" => 100,
                    _ => 25
                };
                _proficiency.SetCap(id, abilityId, capValue);
            }),

            trainStat = new Func<string, string, object?>((entityIdStr, statName) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return null; }
                if (!Enum.TryParse<Tapestry.Engine.Stats.StatType>(statName, true, out var stat))
                {
                    return new { kind = "not_trainable", message = "Unknown stat." };
                }
                var result = _training.TryTrain(id, stat);
                return new { kind = result.Kind.ToString().ToLower(), message = result.Message };
            }),

            setTrainable = new Action<string, bool>((stat, enabled) =>
            {
                _config.SetTrainable(stat.ToLower(), enabled);
            }),

            practice = new Func<string, string, object?>((entityIdStr, abilityId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return null; }
                var result = _training.TryPractice(id, abilityId);
                return new { kind = result.Kind.ToString().ToLower(), message = result.Message };
            }),

            findTrainerInRoom = new Func<string, object?>(entityIdStr =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return null; }
                var match = _training.FindTrainerInRoom(id);
                if (match == null) { return null; }
                return new
                {
                    id = match.TrainerEntityId.ToString(),
                    name = match.TrainerName,
                    tier = match.Tier.ToString().ToLower(),
                    abilities = match.AbilityIds.ToArray()
                };
            })
        };
    }
}
