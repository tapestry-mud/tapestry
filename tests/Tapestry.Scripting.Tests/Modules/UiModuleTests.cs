using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Ui;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class UiModuleTests
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
    public void Panel_ValidTitleSpec_ReturnsStringContainingTitle()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'title', left: 'Hello World' }]
                }]
            })
        ");
        Assert.NotNull(result);
        Assert.Contains("Hello World", result!.ToString());
    }

    [Fact]
    public void Panel_ValidSpec_OutputContainsFrameBorders()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'empty' }]
                }]
            })
        ");
        var str = result!.ToString()!;
        Assert.Contains("|", str);
        Assert.Contains("=", str);
    }

    [Fact]
    public void Panel_TwoSections_OutputContainsMinorRule()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [
                    { rows: [{ type: 'title', left: 'Header' }] },
                    { separatorAbove: 'minor', rows: [{ type: 'empty' }] }
                ]
            })
        ");
        Assert.Contains("-", result!.ToString());
    }

    [Fact]
    public void Panel_CellRowWithFillWidth_ReturnsRenderedContent()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{
                        type: 'cell',
                        cells: [
                            { content: '[Slot]', width: 16 },
                            { content: 'Item Name', width: 'fill' }
                        ]
                    }]
                }]
            })
        ");
        var str = result!.ToString()!;
        Assert.Contains("[Slot]", str);
        Assert.Contains("Item Name", str);
    }

    [Fact]
    public void Panel_ProgressCell_ReturnsRenderedBar()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{
                        type: 'cell',
                        cells: [
                            { content: '  HP  ', width: 8 },
                            { type: 'progress', value: 80, max: 100, width: 22 }
                        ]
                    }]
                }]
            })
        ");
        var str = result!.ToString()!;
        Assert.Contains("[", str);
        Assert.Contains("#", str);
        Assert.Contains("]", str);
    }

    [Fact]
    public void Panel_UnknownRowType_Throws()
    {
        var rt = BuildRuntime();
        var ex = Assert.ThrowsAny<Exception>(() => rt.Execute(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'banana' }]
                }]
            });
        "));
        Assert.Contains("unknown row type", ex.Message);
    }

    [Fact]
    public void Panel_TitleRowMissingLeft_Throws()
    {
        var rt = BuildRuntime();
        Assert.ThrowsAny<Exception>(() => rt.Execute(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'title' }]
                }]
            });
        "));
    }

    [Fact]
    public void Panel_ProgressCellMissingValue_Throws()
    {
        var rt = BuildRuntime();
        Assert.ThrowsAny<Exception>(() => rt.Execute(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{
                        type: 'cell',
                        cells: [{ type: 'progress', max: 100, width: 22 }]
                    }]
                }]
            });
        "));
    }

    [Fact]
    public void Panel_NonObjectArgument_Throws()
    {
        var rt = BuildRuntime();
        Assert.ThrowsAny<Exception>(() => rt.Execute(@"
            tapestry.ui.panel('not an object');
        "));
    }

    [Fact]
    public void Panel_FooterRow_ReturnsRenderedContent()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'footer', content: 'Type help for commands.' }]
                }]
            })
        ");
        Assert.Contains("Type help for commands.", result!.ToString());
    }

    [Fact]
    public void Panel_CustomWidth_ProducesWiderOutput()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                width: 60,
                sections: [{
                    rows: [{ type: 'title', left: 'Narrow' }]
                }]
            })
        ");
        var firstLine = result!.ToString()!.Split("\r\n")[0];
        Assert.Equal(60, TagStripper.VisibleLength(firstLine));
    }

    [Fact]
    public void Panel_TextRow_ReturnsRenderedContent()
    {
        var rt = BuildRuntime();
        var result = rt.Evaluate(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'text', content: '  Gandalf' }]
                }]
            })
        ");
        Assert.Contains("Gandalf", result!.ToString());
    }

    [Fact]
    public void Panel_TextRowMissingContent_Throws()
    {
        var rt = BuildRuntime();
        Assert.ThrowsAny<Exception>(() => rt.Execute(@"
            tapestry.ui.panel({
                sections: [{
                    rows: [{ type: 'text' }]
                }]
            });
        "));
    }
}
