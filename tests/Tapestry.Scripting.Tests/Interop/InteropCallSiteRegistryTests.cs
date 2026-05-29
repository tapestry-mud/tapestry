using FluentAssertions;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Interop;

public class InteropCallSiteRegistryTests
{
    [Fact]
    public void Record_ThenAll_ReturnsRecordedSites()
    {
        var reg = new InteropCallSiteRegistry();
        var site = new InteropCallSite("tapestry-cooking", "tapestry-survival", "getHungerTier", InteropCallKind.Call, "scripts/cook.js", 14);

        reg.Record(site);

        reg.All.Should().ContainSingle().Which.Should().Be(site);
    }

    [Fact]
    public void Clear_RemovesAllSites()
    {
        var reg = new InteropCallSiteRegistry();
        reg.Record(new InteropCallSite("a", "b", "x", InteropCallKind.Call, "f.js", 1));

        reg.Clear();

        reg.All.Should().BeEmpty();
    }
}
