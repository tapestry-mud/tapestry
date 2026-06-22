namespace Tapestry.Engine.Authoring;

public sealed class RoomSpawnData
{
    public string Base { get; set; } = "";
    public RoomSpawnOverrideData? Override { get; set; }
}

public sealed class RoomSpawnOverrideData
{
    public string? FromType { get; set; }
    public string? Name { get; set; }
    public string? Desc { get; set; }
    public int? MaxHp { get; set; }
    public string? Damage { get; set; }
    public List<string> Items { get; set; } = new();
}
