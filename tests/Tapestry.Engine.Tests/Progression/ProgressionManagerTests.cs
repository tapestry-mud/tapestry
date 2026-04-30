using Tapestry.Engine;
using Tapestry.Engine.Progression;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Progression;

public class ProgressionManagerTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private ProgressionManager _progression = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _progression = new ProgressionManager(_world, _eventBus);
    }

    private Entity CreatePlayer(string name = "Travis")
    {
        var entity = new Entity("player", name);
        entity.AddTag("player");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        _world.TrackEntity(entity);
        return entity;
    }

    private TrackDefinition CreateTrack(string name = "combat", int maxLevel = 50)
    {
        return new TrackDefinition
        {
            Name = name,
            MaxLevel = maxLevel,
            XpFormula = level => 100 * level
        };
    }

    [Fact]
    public void RegisterTrack_StoresDefinition()
    {
        Setup();
        var track = CreateTrack();
        _progression.RegisterTrack(track);
        var def = _progression.GetTrackDefinition("combat");
        Assert.NotNull(def);
        Assert.Equal("combat", def.Name);
        Assert.Equal(50, def.MaxLevel);
    }

    [Fact]
    public void RegisterTrack_DuplicateName_Overwrites()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack(maxLevel: 50));
        _progression.RegisterTrack(CreateTrack(maxLevel: 100));
        var def = _progression.GetTrackDefinition("combat");
        Assert.Equal(100, def!.MaxLevel);
    }

    [Fact]
    public void GetTrackDefinition_UnknownTrack_ReturnsNull()
    {
        Setup();
        Assert.Null(_progression.GetTrackDefinition("nonexistent"));
    }

    [Fact]
    public void GetLevel_NewPlayer_ReturnsOne()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        Assert.Equal(1, _progression.GetLevel(player.Id, "combat"));
    }

    [Fact]
    public void GetLevel_UnregisteredTrack_ReturnsZero()
    {
        Setup();
        var player = CreatePlayer();
        Assert.Equal(0, _progression.GetLevel(player.Id, "nonexistent"));
    }

    [Fact]
    public void GetTrackInfo_NewPlayer_ReturnsCorrectDefaults()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.NotNull(info);
        Assert.Equal(0, info.Xp);
        Assert.Equal(1, info.Level);
        Assert.Equal(200, info.XpToNext); // formula: 100 * level, level 2 threshold = 200
        Assert.Equal(0, info.CurrentLevelThreshold);
        Assert.Equal(50, info.MaxLevel);
        Assert.Equal(0, info.Overflow);
    }

    [Fact]
    public void GetTrackInfo_UnregisteredTrack_ReturnsNull()
    {
        Setup();
        var player = CreatePlayer();
        Assert.Null(_progression.GetTrackInfo(player.Id, "nonexistent"));
    }

    [Fact]
    public void GrantExperience_AddsXp()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 50, "combat", "kill");
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(50, info!.Xp);
        Assert.Equal(1, info.Level);
    }

    [Fact]
    public void GrantExperience_TriggersLevelUp()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 200, "combat", "kill");
        Assert.Equal(2, _progression.GetLevel(player.Id, "combat"));
    }

    [Fact]
    public void GrantExperience_MultipleLevsAtOnce()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 500, "combat", "kill");
        Assert.Equal(5, _progression.GetLevel(player.Id, "combat"));
    }

    [Fact]
    public void GrantExperience_CapsAtMaxLevel()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack(maxLevel: 3));
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 9999, "combat", "kill");
        Assert.Equal(3, _progression.GetLevel(player.Id, "combat"));
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(9999, info!.Xp);
        Assert.True(info.Overflow > 0);
    }

    [Fact]
    public void GrantExperience_FiresXpGainedEvent()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        GameEvent? firedEvent = null;
        _eventBus.Subscribe("progression.xp.gained", evt => { firedEvent = evt; });
        _progression.GrantExperience(player.Id, 50, "combat", "kill");
        Assert.NotNull(firedEvent);
        Assert.Equal("combat", firedEvent!.Data["track"]);
        Assert.Equal(50, firedEvent.Data["amount"]);
        Assert.Equal("kill", firedEvent.Data["source"]);
        Assert.Equal(50, firedEvent.Data["newTotal"]);
    }

    [Fact]
    public void GrantExperience_FiresLevelUpEvent()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        GameEvent? levelEvent = null;
        _eventBus.Subscribe("progression.level.up", evt => { levelEvent = evt; });
        _progression.GrantExperience(player.Id, 200, "combat", "kill");
        Assert.NotNull(levelEvent);
        Assert.Equal(1, levelEvent!.Data["oldLevel"]);
        Assert.Equal(2, levelEvent.Data["newLevel"]);
        Assert.Equal("combat", levelEvent.Data["track"]);
    }

    [Fact]
    public void GrantExperience_CallsOnLevelUpCallback()
    {
        Setup();
        var callbackCalled = false;
        var callbackLevel = 0;
        var track = new TrackDefinition
        {
            Name = "combat",
            MaxLevel = 50,
            XpFormula = level => 100 * level,
            OnLevelUp = (entityId, trackName, newLevel) =>
            {
                callbackCalled = true;
                callbackLevel = newLevel;
            }
        };
        _progression.RegisterTrack(track);
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 200, "combat", "kill");
        Assert.True(callbackCalled);
        Assert.Equal(2, callbackLevel);
    }

    [Fact]
    public void GrantExperience_UnregisteredTrack_DoesNothing()
    {
        Setup();
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 100, "nonexistent", "kill");
        Assert.Equal(0, _progression.GetLevel(player.Id, "nonexistent"));
    }

    [Fact]
    public void DeductExperience_ReducesXp()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 150, "combat", "kill");
        _progression.DeductExperience(player.Id, 50, "combat");
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(100, info!.Xp);
    }

    [Fact]
    public void DeductExperience_FloorsAtCurrentLevelThreshold()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 250, "combat", "kill"); // level 2, threshold = 200
        _progression.DeductExperience(player.Id, 999, "combat");
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(2, info!.Level);
        Assert.Equal(200, info.Xp);
    }

    [Fact]
    public void DeductExperience_Level1_FloorsAtZero()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 50, "combat", "kill");
        _progression.DeductExperience(player.Id, 999, "combat");
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(1, info!.Level);
        Assert.Equal(0, info.Xp);
    }

    [Fact]
    public void DeductExperience_FiresXpLostEvent()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 150, "combat", "kill");
        GameEvent? lostEvent = null;
        _eventBus.Subscribe("progression.xp.lost", evt => { lostEvent = evt; });
        _progression.DeductExperience(player.Id, 50, "combat");
        Assert.NotNull(lostEvent);
        Assert.Equal(50, lostEvent!.Data["amount"]);
        Assert.Equal(100, lostEvent.Data["newTotal"]);
    }

    [Fact]
    public void DeductExperience_UnregisteredTrack_DoesNothing()
    {
        Setup();
        var player = CreatePlayer();
        _progression.DeductExperience(player.Id, 100, "nonexistent");
    }

    [Fact]
    public void ResetTrack_ResetsToLevelOneZeroXp()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 500, "combat", "kill");
        Assert.True(_progression.GetLevel(player.Id, "combat") > 1);
        _progression.ResetTrack(player.Id, "combat");
        Assert.Equal(1, _progression.GetLevel(player.Id, "combat"));
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(0, info!.Xp);
    }

    [Fact]
    public void ResetTrack_FiresResetEvent()
    {
        Setup();
        _progression.RegisterTrack(CreateTrack());
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 200, "combat", "kill");
        GameEvent? resetEvent = null;
        _eventBus.Subscribe("progression.track.reset", evt => { resetEvent = evt; });
        _progression.ResetTrack(player.Id, "combat");
        Assert.NotNull(resetEvent);
        Assert.Equal("combat", resetEvent!.Data["track"]);
    }

    [Fact]
    public void GrantExperience_WithXpTable_UsesTableThresholds()
    {
        Setup();
        var track = new TrackDefinition
        {
            Name = "combat",
            MaxLevel = 5,
            XpTable = new[] { 0, 0, 100, 300, 600, 1000 }
        };
        _progression.RegisterTrack(track);
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 100, "combat", "kill");
        Assert.Equal(2, _progression.GetLevel(player.Id, "combat"));
    }

    [Fact]
    public void GrantExperience_WithXpTable_MultiLevel()
    {
        Setup();
        var track = new TrackDefinition
        {
            Name = "combat",
            MaxLevel = 5,
            XpTable = new[] { 0, 0, 100, 300, 600, 1000 }
        };
        _progression.RegisterTrack(track);
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 650, "combat", "kill");
        Assert.Equal(4, _progression.GetLevel(player.Id, "combat"));
    }

    [Fact]
    public void GetTrackInfo_WithXpTable_CorrectXpToNext()
    {
        Setup();
        var track = new TrackDefinition
        {
            Name = "combat",
            MaxLevel = 5,
            XpTable = new[] { 0, 0, 100, 300, 600, 1000 }
        };
        _progression.RegisterTrack(track);
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 150, "combat", "kill");
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(2, info!.Level);
        Assert.Equal(150, info.Xp);
        Assert.Equal(150, info.XpToNext);
        Assert.Equal(100, info.CurrentLevelThreshold);
    }

    [Fact]
    public void DeductExperience_WithXpTable_FloorsAtTableThreshold()
    {
        Setup();
        var track = new TrackDefinition
        {
            Name = "combat",
            MaxLevel = 5,
            XpTable = new[] { 0, 0, 100, 300, 600, 1000 }
        };
        _progression.RegisterTrack(track);
        var player = CreatePlayer();
        _progression.GrantExperience(player.Id, 350, "combat", "kill");
        Assert.Equal(3, _progression.GetLevel(player.Id, "combat"));
        _progression.DeductExperience(player.Id, 999, "combat");
        var info = _progression.GetTrackInfo(player.Id, "combat");
        Assert.Equal(3, info!.Level);
        Assert.Equal(300, info.Xp);
    }
}
