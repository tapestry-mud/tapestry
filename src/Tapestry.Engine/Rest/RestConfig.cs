namespace Tapestry.Engine.Rest;

public class RestConfig
{
    public double RestingMultiplier { get; private set; } = 2.0;
    public double SleepingMultiplier { get; private set; } = 3.0;
    public int MinSleepTicksForWellRested { get; private set; } = 120;

    public void Configure(
        double restingMultiplier = 2.0,
        double sleepingMultiplier = 3.0,
        int minSleepTicksForWellRested = 120)
    {
        RestingMultiplier = restingMultiplier;
        SleepingMultiplier = sleepingMultiplier;
        MinSleepTicksForWellRested = minSleepTicksForWellRested;
    }

    public double GetRestMultiplier(string restState)
    {
        return restState switch
        {
            RestProperties.StateResting => RestingMultiplier,
            RestProperties.StateSleeping => SleepingMultiplier,
            _ => 1.0
        };
    }
}
