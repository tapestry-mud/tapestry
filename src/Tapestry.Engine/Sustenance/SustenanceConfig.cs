namespace Tapestry.Engine.Sustenance;

public class SustenanceConfig
{
    public int DrainAmount { get; private set; } = 1;
    public int DrainCadence { get; private set; } = 300;
    public int ReminderIntervalTicks { get; private set; } = 3000;
    public int TierFullMin { get; private set; } = 67;
    public int TierHungryMin { get; private set; } = 34;

    public void Configure(
        int drainAmount = 1,
        int drainCadence = 300,
        int reminderIntervalTicks = 3000,
        int tierFullMin = 67,
        int tierHungryMin = 34)
    {
        DrainAmount = drainAmount;
        DrainCadence = drainCadence;
        ReminderIntervalTicks = reminderIntervalTicks;
        TierFullMin = tierFullMin;
        TierHungryMin = tierHungryMin;
    }

    public string GetTier(int value)
    {
        if (value >= TierFullMin) { return "full"; }
        if (value >= TierHungryMin) { return "hungry"; }
        return "famished";
    }

    public double GetRegenMultiplier(int value)
    {
        return GetTier(value) switch
        {
            "full" => 1.0,
            "hungry" => 0.5,
            _ => 0.0
        };
    }
}
