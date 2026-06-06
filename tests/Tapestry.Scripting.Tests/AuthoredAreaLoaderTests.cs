using System.IO;
using Tapestry.Engine;
using Tapestry.Scripting.Authoring;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class AuthoredAreaLoaderTests
{
    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tap-areas-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteArea(string root, string areaId, string yaml)
    {
        var areaDir = Path.Combine(root, areaId);
        Directory.CreateDirectory(areaDir);
        File.WriteAllText(Path.Combine(areaDir, "area.yaml"), yaml);
    }

    [Fact]
    public void AuthoredOnlyArea_LoadsWithNullSourcePack()
    {
        var root = TempRoot();
        WriteArea(root, "blight", "area:\n  id: blight\n  name: The Blight\n  theme: Corruption.\n");
        var registry = new AreaRegistry();

        new AuthoredAreaLoader(root, registry).Load();

        var def = registry.Get("blight");
        Assert.NotNull(def);
        Assert.Equal("Corruption.", def!.Theme);
        Assert.Null(def.SourcePack);
    }

    [Fact]
    public void SideCarOverPackedArea_OverlaysFields_AndPreservesSourcePack()
    {
        var root = TempRoot();
        WriteArea(root, "blight", "area:\n  id: blight\n  name: The Blight\n  theme: New theme.\n");
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition
        {
            Id = "blight",
            Name = "The Blight",
            Theme = "Packed theme.",
            SourcePack = "@mallek/legends-forgotten"
        });

        new AuthoredAreaLoader(root, registry).Load();

        var def = registry.Get("blight")!;
        Assert.Equal("New theme.", def.Theme);                      // side-car overlays
        Assert.Equal("@mallek/legends-forgotten", def.SourcePack);  // origin preserved
    }
}
