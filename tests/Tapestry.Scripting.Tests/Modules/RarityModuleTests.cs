using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class RarityModuleTests
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
    public void Register_AndGetTier_ReturnsTierObject()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.rarity.register({ key: 'rare', order: 2, displayText: 'Rare',
                decorators: { left: '-= ', right: ' =-' }, color: 'green', visible: true });
            var tier = tapestry.rarity.getTier('rare');
            tier.key;
        ");
        Assert.Equal("rare", result!.ToString());
    }

    [Fact]
    public void Format_VisibleTier_ReturnsNonEmptyString()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.rarity.register({ key: 'rare', order: 2, displayText: 'Rare',
                decorators: { left: '-= ', right: ' =-' }, color: 'green', visible: true });
            tapestry.rarity.format('rare');
        ");
        Assert.NotNull(result);
        Assert.Contains("Rare", result!.ToString());
    }

    [Fact]
    public void Format_InvisibleTier_ReturnsWhitespace()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.rarity.register({ key: 'common', order: 0, displayText: null,
                decorators: null, color: 'white', visible: false });
            tapestry.rarity.register({ key: 'uncommon', order: 1, displayText: 'Uncommon',
                decorators: { left: '-= ', right: ' =-' }, color: 'white', visible: true });
            tapestry.rarity.format('common');
        ");
        Assert.NotNull(result);
        Assert.Equal(new string(' ', 14), result!.ToString());
    }

    [Fact]
    public void TagWidth_ReturnsZeroWhenNothingRegistered()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"tapestry.rarity.tagWidth();");
        Assert.Equal(0, Convert.ToInt32(result));
    }

    [Fact]
    public void TagWidth_ReturnsMaxRenderedWidth()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.rarity.register({ key: 'uncommon', order: 1, displayText: 'Uncommon',
                decorators: { left: '-= ', right: ' =-' }, color: 'white', visible: true });
            tapestry.rarity.register({ key: 'rare', order: 2, displayText: 'Rare',
                decorators: { left: '-= ', right: ' =-' }, color: 'green', visible: true });
            tapestry.rarity.tagWidth();
        ");
        Assert.Equal(14, Convert.ToInt32(result));
    }
}
