namespace Tapestry.Engine.Rest;

public class RestConfig
{
    // Recovery downtime is dead time. At the previous 2.0/3.0 a level-1 player
    // needed ~43s asleep to refill from a single boss engagement that itself
    // lasted ~22s, so most of a boss fight was spent watching a heal bar. These
    // were also load-bearing in a way multipliers should not be: with a boss
    // that regenerates, resting (2.0) versus sleeping (3.0) was the difference
    // between a winnable fight and a stalled one, which hid a win/lose threshold
    // behind which recovery verb the player happened to type.
    public double RestingMultiplier { get; private set; } = 4.0;
    public double SleepingMultiplier { get; private set; } = 6.0;
    public int MinSleepTicksForWellRested { get; private set; } = 120;

    public void Configure(
        double restingMultiplier = 4.0,
        double sleepingMultiplier = 6.0,
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
