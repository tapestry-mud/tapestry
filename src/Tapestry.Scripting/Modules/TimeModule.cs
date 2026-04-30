using Tapestry.Data;
using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class TimeModule : IJintApiModule
{
    private readonly GameClock _clock;
    private readonly ServerConfig _config;

    public string Namespace => "time";

    public TimeModule(GameClock clock, ServerConfig config)
    {
        _clock = clock;
        _config = config;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            hour = new Func<int>(() => _clock.CurrentHour),
            period = new Func<string>(() => _clock.CurrentPeriod.ToString().ToLower()),
            dayCount = new Func<int>(() => _clock.DayCount),
            ticksPerHour = new Func<int>(() => _config.Game.TicksPerGameHour)
        };
    }
}
