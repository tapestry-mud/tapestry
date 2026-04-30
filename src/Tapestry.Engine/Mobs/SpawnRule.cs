namespace Tapestry.Engine.Mobs;

public class RareSpawnConfig
{
    public string Mob { get; set; } = "";
    public double Chance { get; set; }
}

public class SpawnRule
{
    public string Room { get; set; } = "";
    public string Mob { get; set; } = "";
    public int Count { get; set; } = 1;
    public RareSpawnConfig? Rare { get; set; }
    public int? RespawnOverride { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class AreaSpawnConfig
{
    public string Area { get; set; } = "";
    public int ResetInterval { get; set; } = 300;
    public List<SpawnRule> Spawns { get; set; } = new();
}
