namespace Tapestry.Server.Persistence;

public static class SaveMigrations
{
    public const int CurrentVersion = 1;

    public static readonly Dictionary<int, Func<Dictionary<object, object>, Dictionary<object, object>>> Migrations = new()
    {
        // Future migrations go here:
        // { 2, MigrateV1ToV2 },
    };
}
