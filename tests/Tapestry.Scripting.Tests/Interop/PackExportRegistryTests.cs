using System.Linq;
using FluentAssertions;
using Jint.Native;
using Tapestry.Scripting.Interop;
using Xunit;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Tests.Interop;

public class PackExportRegistryTests
{
    private static JsValue Fn(JintEngine e) => e.Evaluate("(function () { return 1; })");

    [Fact]
    public void Register_ThenResolve_ReturnsEntry()
    {
        var e = new JintEngine();
        var reg = new PackExportRegistry();
        var entry = new ExportEntry("tapestry-survival", "getHungerTier", Fn(e),
            "Hunger tier", new[] { "entityId:entity" }, "string", "query", new[] { "all" });

        reg.Register(entry);

        reg.TryResolve("tapestry-survival", "getHungerTier", out var found).Should().BeTrue();
        found.Name.Should().Be("getHungerTier");
        found.Kind.Should().Be("query");
    }

    [Fact]
    public void Register_DuplicatePackName_Throws()
    {
        var e = new JintEngine();
        var reg = new PackExportRegistry();
        var entry = new ExportEntry("tapestry-survival", "getHungerTier", Fn(e),
            "", System.Array.Empty<string>(), "string", "query", new[] { "all" });
        reg.Register(entry);

        var act = () => reg.Register(entry);

        act.Should().Throw<InteropException>().WithMessage("*already*");
    }

    [Fact]
    public void Has_TrueOnlyForRegisteredExport()
    {
        var e = new JintEngine();
        var reg = new PackExportRegistry();
        reg.Register(new ExportEntry("tapestry-survival", "getHungerTier", Fn(e),
            "", System.Array.Empty<string>(), "string", "query", new[] { "all" }));

        reg.Has("tapestry-survival", "getHungerTier").Should().BeTrue();
        reg.Has("tapestry-survival", "applyWellFedBuff").Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsEverything()
    {
        var e = new JintEngine();
        var reg = new PackExportRegistry();
        reg.Register(new ExportEntry("tapestry-survival", "getHungerTier", Fn(e),
            "", System.Array.Empty<string>(), "string", "query", new[] { "all" }));
        reg.Register(new ExportEntry("tapestry-survival", "applyWellFedBuff", Fn(e),
            "", System.Array.Empty<string>(), "undefined", "command", new[] { "all" }));

        reg.GetAll().Should().HaveCount(2);
    }
}
