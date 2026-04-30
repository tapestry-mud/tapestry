using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class AbilitiesModuleAlignmentTests
{
    [Fact]
    public void Register_WithAlignmentRangeMax_SetsField()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var sp = services.BuildServiceProvider();
        var rt = sp.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var registry = sp.GetRequiredService<AbilityRegistry>();

        rt.Execute(@"
            tapestry.abilities.register({
                id: 'invoke_darkness',
                name: 'Invoke',
                alignment_range: { max: -700 }
            });
        ");

        var def = registry.Get("invoke_darkness");
        Assert.NotNull(def!.AlignmentRange);
        Assert.Null(def.AlignmentRange!.Min);
        Assert.Equal(-700, def.AlignmentRange.Max);
    }

    [Fact]
    public void Register_WithAlignmentRangeBuckets_ResolvesToNumeric()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var sp = services.BuildServiceProvider();
        var rt = sp.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var registry = sp.GetRequiredService<AbilityRegistry>();

        rt.Execute(@"
            tapestry.abilities.register({
                id: 'evil_only',
                name: 'Evil Only',
                alignment_range: { buckets: ['evil'] }
            });
        ");

        var def = registry.Get("evil_only");
        Assert.NotNull(def!.AlignmentRange);
        Assert.Equal(-350, def.AlignmentRange!.Max);  // default EvilThreshold
        Assert.Null(def.AlignmentRange.Min);
    }
}
