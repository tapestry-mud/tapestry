using Tapestry.Data;
using Tapestry.Shared;

namespace Tapestry.Engine;

public enum TimePeriod { Dawn, Day, Dusk, Night }

public class GameClock
{
    private readonly EventBus _eventBus;
    private readonly ServerConfig _config;
    private long _tickCount;

    public int CurrentHour { get; private set; }
    public TimePeriod CurrentPeriod { get; private set; } = TimePeriod.Night;
    public int DayCount { get; private set; }

    public GameClock(EventBus eventBus, ServerConfig config)
    {
        _eventBus = eventBus;
        _config = config;
        CurrentPeriod = DeterminePeriod(CurrentHour);
    }

    public void Tick()
    {
        _tickCount++;
        if (_tickCount % _config.Game.TicksPerGameHour != 0) { return; }

        var previousPeriod = CurrentPeriod;
        CurrentHour++;
        if (CurrentHour > 23)
        {
            CurrentHour = 0;
            DayCount++;
        }

        CurrentPeriod = DeterminePeriod(CurrentHour);

        if (CurrentPeriod != previousPeriod)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "time.period.change",
                Data = new Dictionary<string, object?>
                {
                    ["period"] = CurrentPeriod.ToString().ToLower(),
                    ["previousPeriod"] = previousPeriod.ToString().ToLower(),
                    ["hour"] = CurrentHour
                }
            });
        }

        _eventBus.Publish(new GameEvent
        {
            Type = "time.hour.change",
            Data = new Dictionary<string, object?>
            {
                ["hour"] = CurrentHour,
                ["period"] = CurrentPeriod.ToString().ToLower(),
                ["dayCount"] = DayCount
            }
        });
    }

    private TimePeriod DeterminePeriod(int hour)
    {
        var b = _config.Game.PeriodBoundaries;
        // b = [dawn, day, dusk, night] boundary hours
        if (hour >= b[3]) { return TimePeriod.Night; }
        if (hour >= b[2]) { return TimePeriod.Dusk; }
        if (hour >= b[1]) { return TimePeriod.Day; }
        if (hour >= b[0]) { return TimePeriod.Dawn; }
        return TimePeriod.Night;
    }
}
