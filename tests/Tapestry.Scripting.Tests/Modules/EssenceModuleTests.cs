using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class EssenceModuleTests
{
    private JintRuntime BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return rt;
    }

    [Fact]
    public void Register_AndGetEssence_ReturnsEssenceObject()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.essence.register({ key: 'fire', glyph: '^', color: 'red' });
            var e = tapestry.essence.getEssence('fire');
            e.glyph;
        ");
        Assert.Equal("^", result!.ToString());
    }

    [Fact]
    public void Format_KnownKey_ReturnsColoredGlyphInParens()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.essence.register({ key: 'fire', glyph: '^', color: 'red' });
            tapestry.essence.format('fire');
        ");
        Assert.NotNull(result);
        Assert.Contains("(^)", result!.ToString());
    }

    [Fact]
    public void Format_NullKey_ReturnsEmptyString()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.essence.register({ key: 'fire', glyph: '^', color: 'red' });
            tapestry.essence.format(null);
        ");
        Assert.Equal(string.Empty, result!.ToString());
    }

    [Fact]
    public void Format_UnknownKey_ReturnsEmptyString()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"tapestry.essence.format('void');");
        Assert.Equal(string.Empty, result!.ToString());
    }
}
