using Tapestry.Engine.Effects;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Effects;

public class EffectManagerTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private EffectManager _effects = null!;
    private Entity _player = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _effects = new EffectManager(_world, _eventBus);

        _player = new Entity("player", "Travis");
        _player.Stats.BaseMaxHp = 100;
        _player.Stats.Hp = 100;
        _player.Stats.BaseStrength = 10;
        _player.Stats.BaseDexterity = 10;
        _world.TrackEntity(_player);
    }

    private Entity CreateEntity()
    {
        var entity = new Entity("npc", "Mob");
        _world.TrackEntity(entity);
        return entity;
    }

    [Fact]
    public void TryApply_AddsEffect()
    {
        Setup();
        var effect = new ActiveEffect
        {
            Id = "sanctuary",
            SourceEntityId = _player.Id,
            TargetEntityId = _player.Id,
            RemainingPulses = 100,
            Flags = new List<string> { "sanctuary" }
        };
        _effects.TryApply(effect);
        Assert.True(_effects.HasEffect(_player.Id, "sanctuary"));
        Assert.True(_player.HasTag("sanctuary"));
    }

    [Fact]
    public void TryApply_AddsStatModifiers()
    {
        Setup();
        var effect = new ActiveEffect
        {
            Id = "bless",
            SourceEntityId = _player.Id,
            TargetEntityId = _player.Id,
            RemainingPulses = 50,
            StatModifiers = new List<StatModifier>
            {
                new("effect:bless", StatType.Strength, 5)
            }
        };
        _effects.TryApply(effect);
        Assert.Equal(15, _player.Stats.Strength);
    }

    [Fact]
    public void TickPulse_DecrementsDuration()
    {
        Setup();
        var effect = new ActiveEffect
        {
            Id = "sanctuary",
            TargetEntityId = _player.Id,
            RemainingPulses = 3,
            Flags = new List<string> { "sanctuary" }
        };
        _effects.TryApply(effect);
        _effects.TickPulse();
        Assert.True(_effects.HasEffect(_player.Id, "sanctuary"));
        _effects.TickPulse();
        Assert.True(_effects.HasEffect(_player.Id, "sanctuary"));
        _effects.TickPulse();
        Assert.False(_effects.HasEffect(_player.Id, "sanctuary"));
        Assert.False(_player.HasTag("sanctuary"));
    }

    [Fact]
    public void TickPulse_ExpiresEffect_RemovesStatModifiers()
    {
        Setup();
        var effect = new ActiveEffect
        {
            Id = "bless",
            TargetEntityId = _player.Id,
            RemainingPulses = 1,
            StatModifiers = new List<StatModifier>
            {
                new("effect:bless", StatType.Strength, 5)
            }
        };
        _effects.TryApply(effect);
        Assert.Equal(15, _player.Stats.Strength);
        _effects.TickPulse();
        Assert.Equal(10, _player.Stats.Strength);
    }

    [Fact]
    public void TickPulse_PublishesExpiredEvent()
    {
        Setup();
        string? firedEventType = null;
        _eventBus.Subscribe("effect.expired", (evt) => { firedEventType = evt.Type; });
        var effect = new ActiveEffect
        {
            Id = "bless",
            TargetEntityId = _player.Id,
            RemainingPulses = 1
        };
        _effects.TryApply(effect);
        _effects.TickPulse();
        Assert.Equal("effect.expired", firedEventType);
    }

    // Apply_DuplicateId_RefreshesDuration — removed: Apply() replaced by TryApply() which gates on duplicate ID.

    [Fact]
    public void Remove_RemovesEffectAndCleansUp()
    {
        Setup();
        var effect = new ActiveEffect
        {
            Id = "sanctuary",
            TargetEntityId = _player.Id,
            RemainingPulses = 100,
            Flags = new List<string> { "sanctuary" },
            StatModifiers = new List<StatModifier>
            {
                new("effect:sanctuary", StatType.MaxHp, 50)
            }
        };
        _effects.TryApply(effect);
        Assert.True(_player.HasTag("sanctuary"));
        Assert.Equal(150, _player.Stats.MaxHp);

        _effects.Remove(_player.Id, "sanctuary");
        Assert.False(_effects.HasEffect(_player.Id, "sanctuary"));
        Assert.False(_player.HasTag("sanctuary"));
        Assert.Equal(100, _player.Stats.MaxHp);
    }

    [Fact]
    public void Remove_PublishesRemovedEvent()
    {
        Setup();
        string? firedEventType = null;
        _eventBus.Subscribe("effect.removed", (evt) => { firedEventType = evt.Type; });
        var effect = new ActiveEffect { Id = "test", TargetEntityId = _player.Id, RemainingPulses = 100 };
        _effects.TryApply(effect);
        _effects.Remove(_player.Id, "test");
        Assert.Equal("effect.removed", firedEventType);
    }

    [Fact]
    public void RemoveByFlag_RemovesMatchingEffects()
    {
        Setup();
        _effects.TryApply(new ActiveEffect
        {
            Id = "poison",
            TargetEntityId = _player.Id,
            RemainingPulses = 100,
            Flags = new List<string> { "is_poisoned", "no_heal" }
        });
        _effects.RemoveByFlag(_player.Id, "is_poisoned");
        Assert.False(_effects.HasEffect(_player.Id, "poison"));
        Assert.False(_player.HasTag("is_poisoned"));
        Assert.False(_player.HasTag("no_heal"));
    }

    [Fact]
    public void GetActive_ReturnsAllActiveEffects()
    {
        Setup();
        _effects.TryApply(new ActiveEffect { Id = "bless", TargetEntityId = _player.Id, RemainingPulses = 100 });
        _effects.TryApply(new ActiveEffect { Id = "sanctuary", TargetEntityId = _player.Id, RemainingPulses = 50 });
        var active = _effects.GetActive(_player.Id);
        Assert.Equal(2, active.Count);
    }

    [Fact]
    public void PermanentEffect_NeverExpires()
    {
        Setup();
        _effects.TryApply(new ActiveEffect
        {
            Id = "permanent_buff",
            TargetEntityId = _player.Id,
            RemainingPulses = -1,
            Flags = new List<string> { "buffed" }
        });
        for (var i = 0; i < 100; i++)
        {
            _effects.TickPulse();
        }
        Assert.True(_effects.HasEffect(_player.Id, "permanent_buff"));
        Assert.True(_player.HasTag("buffed"));
    }

    [Fact]
    public void TryApply_PublishesAppliedEvent()
    {
        Setup();
        string? firedEventType = null;
        _eventBus.Subscribe("effect.applied", (evt) => { firedEventType = evt.Type; });
        _effects.TryApply(new ActiveEffect { Id = "test", TargetEntityId = _player.Id, RemainingPulses = 10 });
        Assert.Equal("effect.applied", firedEventType);
    }

    [Fact]
    public void TryApply_WhenEffectAlreadyExists_ReturnsFalse()
    {
        Setup();
        var entity = CreateEntity();
        var effect1 = new ActiveEffect
        {
            Id = "shielded",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = entity.Id,
            RemainingPulses = 30
        };
        var effect2 = new ActiveEffect
        {
            Id = "shielded",
            SourceEntityId = Guid.NewGuid(),
            TargetEntityId = entity.Id,
            RemainingPulses = 60
        };

        var result1 = _effects.TryApply(effect1);
        var result2 = _effects.TryApply(effect2);

        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public void TickPulse_DecrementsRemainingPulses()
    {
        Setup();
        var entity = CreateEntity();
        _effects.TryApply(new ActiveEffect
        {
            Id = "sanctuary",
            TargetEntityId = entity.Id,
            RemainingPulses = 3
        });

        _effects.TickPulse();
        var active = _effects.GetActive(entity.Id);
        Assert.Single(active);
        Assert.Equal(2, active[0].RemainingPulses);
    }

    [Fact]
    public void TickPulse_RemovesExpiredEffects()
    {
        Setup();
        var entity = CreateEntity();
        _effects.TryApply(new ActiveEffect
        {
            Id = "sanctuary",
            TargetEntityId = entity.Id,
            RemainingPulses = 1,
            Flags = new List<string> { "sanctuary" }
        });
        Assert.True(entity.HasTag("sanctuary"));

        _effects.TickPulse();
        Assert.False(_effects.HasEffect(entity.Id, "sanctuary"));
        Assert.False(entity.HasTag("sanctuary"));
    }

    [Fact]
    public void TickPulse_PermanentEffects_NeverExpire()
    {
        Setup();
        var entity = CreateEntity();
        _effects.TryApply(new ActiveEffect
        {
            Id = "buff",
            TargetEntityId = entity.Id,
            RemainingPulses = -1
        });

        for (var i = 0; i < 100; i++)
        {
            _effects.TickPulse();
        }
        Assert.True(_effects.HasEffect(entity.Id, "buff"));
    }
}
