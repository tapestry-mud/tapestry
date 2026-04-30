namespace Tapestry.Engine.Training;

public class TrainingConfig
{
    public bool RequireSafeRoomForStats { get; private set; } = false;
    private List<string> _trainableStats = new()
    {
        "strength", "intelligence", "wisdom", "dexterity", "constitution", "luck"
    };
    public IReadOnlyList<string> TrainableStats => _trainableStats;
    public int CatchUpBoost { get; private set; } = 5;

    public void Configure(bool requireSafeRoom, IReadOnlyList<string>? trainableStats, int catchUpBoost)
    {
        RequireSafeRoomForStats = requireSafeRoom;
        if (trainableStats != null && trainableStats.Count > 0)
        {
            _trainableStats = new List<string>(trainableStats);
        }
        CatchUpBoost = catchUpBoost;
    }

    public void SetTrainable(string stat, bool enabled)
    {
        if (enabled && !_trainableStats.Contains(stat)) { _trainableStats.Add(stat); }
        else if (!enabled) { _trainableStats.Remove(stat); }
    }
}
