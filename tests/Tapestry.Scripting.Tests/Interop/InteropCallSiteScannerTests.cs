using System.Linq;
using FluentAssertions;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Interop;

public class InteropCallSiteScannerTests
{
    [Fact]
    public void Extract_LiteralCall_RecordsNamespacedSite()
    {
        const string src = "tapestry.packs.call('@tapestry/survival', 'getHungerTier', id);";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "scripts/cook.js");

        sites.Should().ContainSingle();
        var s = sites[0];
        s.CallerPack.Should().Be("tapestry-cooking");
        s.TargetPack.Should().Be("tapestry-survival"); // normalized from @tapestry/survival
        s.ExportName.Should().Be("getHungerTier");
        s.Kind.Should().Be(InteropCallKind.Call);
        s.SourceFile.Should().Be("scripts/cook.js");
        s.Line.Should().Be(1);
    }

    [Fact]
    public void Extract_LiteralHas_RecordsSite()
    {
        const string src = "if (tapestry.packs.has('@tapestry/survival', 'foo')) {}";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "f.js");

        sites.Should().ContainSingle();
        sites[0].TargetPack.Should().Be("tapestry-survival");
        sites[0].ExportName.Should().Be("foo");
        sites[0].Kind.Should().Be(InteropCallKind.Has);
    }

    [Fact]
    public void Extract_NonLiteralPackArg_IsSkipped()
    {
        const string src = "var p = 'x'; tapestry.packs.call(p, 'foo');";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "f.js");

        sites.Should().BeEmpty();
    }

    [Fact]
    public void Extract_NonLiteralExportArg_IsSkipped()
    {
        const string src = "tapestry.packs.call('@tapestry/survival', someVar);";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "f.js");

        sites.Should().BeEmpty();
    }

    [Fact]
    public void Extract_UnrelatedCall_IsIgnored()
    {
        const string src = "foo.bar.call('a', 'b'); tapestry.world.getRoom('r1');";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "f.js");

        sites.Should().BeEmpty();
    }

    [Fact]
    public void Extract_CapturesCorrectLineNumber()
    {
        const string src = "function cook(id) {\n  return tapestry.packs.call('@tapestry/survival', 'getHungerTier', id);\n}";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "scripts/cook.js");

        sites.Should().ContainSingle();
        sites[0].Line.Should().Be(2);
    }

    [Fact]
    public void Extract_NestedCallInsideArguments_IsFound()
    {
        // A call whose own argument is another interop call — recursion must reach it.
        const string src = "tapestry.packs.call('@tapestry/a', 'wrap', tapestry.packs.call('@tapestry/b', 'inner'));";

        var sites = InteropCallSiteScanner.Extract(src, "tapestry-cooking", "f.js");

        sites.Select(s => (s.TargetPack, s.ExportName))
            .Should().BeEquivalentTo(new[] { ("tapestry-a", "wrap"), ("tapestry-b", "inner") });
    }

    [Fact]
    public void Extract_SyntaxError_ReturnsEmptyWithoutThrowing()
    {
        const string src = "function broken( { tapestry.packs.call('@tapestry/survival', 'x'";

        var act = () => InteropCallSiteScanner.Extract(src, "tapestry-cooking", "f.js");

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void Extract_RequireLiteral_RecordsRequireSite()
    {
        var sites = InteropCallSiteScanner.Extract(
            "var t = tapestry.packs.require('@tapestry/survival');",
            "tapestry-cooking", "scripts/a.js");

        sites.Should().ContainSingle();
        sites[0].Kind.Should().Be(InteropCallKind.Require);
        sites[0].TargetPack.Should().Be("tapestry-survival");
        sites[0].ExportName.Should().Be("");
        sites[0].Line.Should().Be(1);
    }

    [Fact]
    public void Extract_RequireDynamic_IsSkipped()
    {
        var sites = InteropCallSiteScanner.Extract(
            "var p = '@tapestry/survival'; var t = tapestry.packs.require(p);",
            "tapestry-cooking", "scripts/a.js");

        sites.Should().BeEmpty();
    }
}
