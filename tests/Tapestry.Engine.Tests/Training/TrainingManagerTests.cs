using Tapestry.Engine.Abilities;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Training;

namespace Tapestry.Engine.Tests.Training;

public class TrainingManagerTests
{
    private World _world = null!;
    private AbilityRegistry _abilityRegistry = null!;
    private ProficiencyManager _proficiency = null!;
    private RaceRegistry _races = null!;
    private TrainingConfig _config = null!;
    private TrainingManager _training = null!;
    private Entity _player = null!;

    private void Setup(bool requireSafeRoom = false)
    {
        _world = new World();
        _abilityRegistry = new AbilityRegistry();
        _proficiency = new ProficiencyManager(_world, _abilityRegistry);
        _races = new RaceRegistry();
        _config = new TrainingConfig();
        if (requireSafeRoom) { _config.Configure(true, null, 5); }
        _training = new TrainingManager(_world, _proficiency, _races, _config, _abilityRegistry);

        _abilityRegistry.Register(new AbilityDefinition { Id = "dodge", Name = "Dodge", ProficiencyGainChance = 1.0 });
        _abilityRegistry.Register(new AbilityDefinition { Id = "parry", Name = "Parry", ProficiencyGainChance = 1.0 });

        _player = new Entity("player", "Tester");
        _player.AddTag("player");
        _world.TrackEntity(_player);
    }

    private Entity SpawnTrainer(string roomId, CapTier tier, string[] abilities)
    {
        var room = _world.GetRoom(roomId) ?? throw new Exception($"Room {roomId} not found");
        var trainer = new Entity("npc", "Thom the Swordmaster");
        trainer.AddTag("skill_trainer");
        trainer.SetProperty(TrainingProperties.TrainerConfigKey, new TrainerConfig(tier, abilities));
        trainer.LocationRoomId = roomId;
        room.AddEntity(trainer);
        _world.TrackEntity(trainer);
        return trainer;
    }

    private void PlacePlayerInRoom(string roomId)
    {
        _world.AddRoom(new Room(roomId, "Training Grounds", ""));
        _player.LocationRoomId = roomId;
        _world.GetRoom(roomId)!.AddEntity(_player);
    }

    // ── GetTrainsAvailable / GrantTrains ──────────────────────────────

    [Fact]
    public void GetTrainsAvailable_DefaultsToZero()
    {
        Setup();
        Assert.Equal(0, _training.GetTrainsAvailable(_player.Id));
    }

    [Fact]
    public void GrantTrains_AddsToAvailable()
    {
        Setup();
        _training.GrantTrains(_player.Id, 5);
        Assert.Equal(5, _training.GetTrainsAvailable(_player.Id));
    }

    [Fact]
    public void GrantTrains_Accumulates()
    {
        Setup();
        _training.GrantTrains(_player.Id, 3);
        _training.GrantTrains(_player.Id, 4);
        Assert.Equal(7, _training.GetTrainsAvailable(_player.Id));
    }

    // ── FindTrainerInRoom ─────────────────────────────────────────────

    [Fact]
    public void FindTrainerInRoom_ReturnsNullWhenNoTrainerPresent()
    {
        Setup();
        PlacePlayerInRoom("room1");
        Assert.Null(_training.FindTrainerInRoom(_player.Id));
    }

    [Fact]
    public void FindTrainerInRoom_ReturnsMatchWhenTrainerPresent()
    {
        Setup();
        PlacePlayerInRoom("room1");
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge", "parry" });
        var match = _training.FindTrainerInRoom(_player.Id);
        Assert.NotNull(match);
        Assert.Equal(CapTier.Apprentice, match!.Tier);
        Assert.Contains("dodge", match.AbilityIds);
    }

    // ── TryPractice ───────────────────────────────────────────────────

    [Fact]
    public void TryPractice_RefusesWhenAbilityNotLearned()
    {
        Setup();
        PlacePlayerInRoom("room1");
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge" });
        var result = _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(PracticeResultKind.NotLearned, result.Kind);
    }

    [Fact]
    public void TryPractice_RefusesWhenNoTrainerInRoom()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge");
        var result = _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(PracticeResultKind.NoTrainer, result.Kind);
    }

    [Fact]
    public void TryPractice_RefusesWhenTrainerCannotTeachAbility()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge");
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "parry" });
        var result = _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(PracticeResultKind.CannotTeach, result.Kind);
    }

    [Fact]
    public void TryPractice_RefusesWhenPlayerAlreadyAtOrAboveTier()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge");
        _proficiency.SetCap(_player.Id, "dodge", 50);
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge" });
        var result = _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(PracticeResultKind.AlreadyAtOrAboveTier, result.Kind);
    }

    [Fact]
    public void TryPractice_RefusesWhenTrainerTierSkipsAhead()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge");
        SpawnTrainer("room1", CapTier.Journeyman, new[] { "dodge" });
        var result = _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(PracticeResultKind.TierSkip, result.Kind);
    }

    [Fact]
    public void TryPractice_SucceedsAndAdvancesCap()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge");
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge" });
        var result = _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(PracticeResultKind.Success, result.Kind);
        Assert.Equal(50, _proficiency.GetCap(_player.Id, "dodge"));
    }

    [Fact]
    public void TryPractice_CatchUpBoostAppliesWhenBelowPrevTierCap()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge", 5);
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge" });
        _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(10, _proficiency.GetProficiency(_player.Id, "dodge"));
    }

    [Fact]
    public void TryPractice_CatchUpClampsToPreTierCap()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge", 22);
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge" });
        _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(25, _proficiency.GetProficiency(_player.Id, "dodge"));
    }

    [Fact]
    public void TryPractice_NoCatchUpWhenAtOrAbovePrevTierCap()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _proficiency.Learn(_player.Id, "dodge", 25);
        SpawnTrainer("room1", CapTier.Apprentice, new[] { "dodge" });
        _training.TryPractice(_player.Id, "dodge");
        Assert.Equal(25, _proficiency.GetProficiency(_player.Id, "dodge"));
    }

    // ── TryTrain ──────────────────────────────────────────────────────

    [Fact]
    public void TryTrain_RefusesWhenNoTrainsAvailable()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _player.SetProperty("race", "folk");
        _races.Register(new RaceDefinition
        {
            Id = "folk", Name = "Andoran",
            StatCaps = new Dictionary<StatType, int> { { StatType.Strength, 25 } }
        });
        var result = _training.TryTrain(_player.Id, StatType.Strength);
        Assert.Equal(StatTrainResultKind.NoTrains, result.Kind);
    }

    [Fact]
    public void TryTrain_RefusesWhenAtRaceCap()
    {
        Setup();
        PlacePlayerInRoom("room1");
        _races.Register(new RaceDefinition
        {
            Id = "folk", Name = "Andoran",
            StatCaps = new Dictionary<StatType, int> { { StatType.Strength, 14 } }
        });
        _player.SetProperty("race", "folk");
        _player.Stats.BaseStrength = 14;
        _training.GrantTrains(_player.Id, 5);
        var result = _training.TryTrain(_player.Id, StatType.Strength);
        Assert.Equal(StatTrainResultKind.AtRaceCap, result.Kind);
    }

    [Fact]
    public void TryTrain_RefusesWhenStatNotTrainable()
    {
        Setup();
        _config.Configure(false, new[] { "intelligence" }, 5);
        PlacePlayerInRoom("room1");
        _training.GrantTrains(_player.Id, 5);
        var result = _training.TryTrain(_player.Id, StatType.Strength);
        Assert.Equal(StatTrainResultKind.NotTrainable, result.Kind);
    }

    [Fact]
    public void TryTrain_RefusesInUnsafeRoomWhenFlagSet()
    {
        Setup(requireSafeRoom: true);
        PlacePlayerInRoom("room1");
        _training.GrantTrains(_player.Id, 5);
        var result = _training.TryTrain(_player.Id, StatType.Strength);
        Assert.Equal(StatTrainResultKind.UnsafeRoom, result.Kind);
    }

    [Fact]
    public void TryTrain_SucceedsInAnyRoomWhenFlagFalse()
    {
        Setup(requireSafeRoom: false);
        PlacePlayerInRoom("room1");
        _races.Register(new RaceDefinition
        {
            Id = "folk", Name = "Andoran",
            StatCaps = new Dictionary<StatType, int> { { StatType.Strength, 25 } }
        });
        _player.SetProperty("race", "folk");
        _player.Stats.BaseStrength = 10;
        _training.GrantTrains(_player.Id, 3);
        var result = _training.TryTrain(_player.Id, StatType.Strength);
        Assert.Equal(StatTrainResultKind.Success, result.Kind);
        Assert.Equal(11, result.NewValue);
        Assert.Equal(2, _training.GetTrainsAvailable(_player.Id));
    }
}
