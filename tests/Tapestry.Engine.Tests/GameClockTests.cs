using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class GameClockTests
{
    private static (GameClock, EventBus, ServerConfig) Create(int ticksPerHour = 10)
    {
        var eventBus = new EventBus();
        var config = new ServerConfig();
        config.Game.TicksPerGameHour = ticksPerHour;
        var clock = new GameClock(eventBus, config);
        return (clock, eventBus, config);
    }

    [Fact]
    public void Tick_AdvancesHour_EveryTicksPerGameHour()
    {
        var (clock, _, _) = Create(ticksPerHour: 5);

        for (var i = 0; i < 5; i++) { clock.Tick(); }

        clock.CurrentHour.Should().Be(1);
    }

    [Fact]
    public void Tick_DoesNotAdvanceHour_BeforeInterval()
    {
        var (clock, _, _) = Create(ticksPerHour: 5);

        for (var i = 0; i < 4; i++) { clock.Tick(); }

        clock.CurrentHour.Should().Be(0);
    }

    [Fact]
    public void Tick_WrapsHour_AfterMidnight()
    {
        var (clock, _, _) = Create(ticksPerHour: 1);

        // Advance through 24 hours (hour 0..23 -> wraps to 0)
        for (var i = 0; i < 24; i++) { clock.Tick(); }

        clock.CurrentHour.Should().Be(0);
        clock.DayCount.Should().Be(1);
    }

    [Fact]
    public void Tick_FiresHourChangeEvent_WithCorrectPayload()
    {
        var (clock, eventBus, _) = Create(ticksPerHour: 3);
        GameEvent? captured = null;
        eventBus.Subscribe("time.hour.change", e => { captured = e; });

        clock.Tick();
        clock.Tick();
        clock.Tick();

        captured.Should().NotBeNull();
        Convert.ToInt32(captured!.Data["hour"]).Should().Be(1);
        captured.Data.ContainsKey("period").Should().BeTrue();
        captured.Data.ContainsKey("dayCount").Should().BeTrue();
    }

    [Fact]
    public void Tick_FiresPeriodChangeEvent_OnlyOnTransition()
    {
        // PeriodBoundaries = [5, 8, 18, 20] -- Dawn at hour 5
        var (clock, eventBus, _) = Create(ticksPerHour: 1);
        var periodChanges = new List<GameEvent>();
        eventBus.Subscribe("time.period.change", e => { periodChanges.Add(e); });

        // Advance to hour 5 (Dawn boundary)
        for (var i = 0; i < 5; i++) { clock.Tick(); }

        periodChanges.Should().HaveCount(1);
        periodChanges[0].Data["period"].Should().Be("dawn");
        periodChanges[0].Data["previousPeriod"].Should().Be("night");
    }

    [Fact]
    public void Tick_DoesNotFirePeriodChange_WithinSamePeriod()
    {
        // PeriodBoundaries = [5, 8, 18, 20] -- Day is hours 8-17
        var (clock, eventBus, _) = Create(ticksPerHour: 1);
        var periodChanges = new List<GameEvent>();

        // Advance to hour 8 (Day boundary), clearing initial changes
        for (var i = 0; i < 8; i++) { clock.Tick(); }

        eventBus.Subscribe("time.period.change", e => { periodChanges.Add(e); });

        // Advance through hours 9-17 (still Day, no period change)
        for (var i = 0; i < 9; i++) { clock.Tick(); }

        periodChanges.Should().BeEmpty();
    }
}
