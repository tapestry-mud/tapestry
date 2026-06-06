using Tapestry.Engine;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class PackLoaderAreaStampTests
{
    [Fact]
    public void LoadedPackArea_IsStampedWithSourcePack()
    {
        var registry = new AreaRegistry();
        var yaml = "area:\n  id: zone\n  name: Zone\n";
        var def = YamlContentLoader.LoadAreaDefinition(yaml);

        def.SourcePack = "@mallek/legends-forgotten";
        registry.Register(def);

        Assert.Equal("@mallek/legends-forgotten", registry.Get("zone")!.SourcePack);
    }
}
