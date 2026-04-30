using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class WeatherModule : IJintApiModule
{
    private readonly WeatherService _weather;

    public string Namespace => "weather";

    public WeatherModule(WeatherService weather)
    {
        _weather = weather;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            current = new Func<string, string>(areaId => _weather.GetCurrentWeather(areaId)),
            set = new Action<string, string>((areaId, state) => _weather.SetWeather(areaId, state))
        };
    }
}
