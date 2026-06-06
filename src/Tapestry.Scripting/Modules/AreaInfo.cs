namespace Tapestry.Scripting.Modules;

public sealed record AreaInfo(
    string Id, string Name, string Short, string Description, string Theme, string Lore,
    int[] LevelRange, int ResetInterval, string? SourcePack, bool SideCar, bool Exists)
{
    public static AreaInfo Missing(string id) =>
        new(id, "", "", "", "", "", new[] { 1, 99 }, 0, null, false, false);
}
