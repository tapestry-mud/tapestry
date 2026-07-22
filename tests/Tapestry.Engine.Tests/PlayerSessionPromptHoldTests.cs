using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class PlayerSessionPromptHoldTests
{
    private static PlayerSession NewSession()
    {
        return new PlayerSession(new FakeConnection(), new Entity("player", "Rand"));
    }

    [Fact]
    public void IsPromptHeld_FalseByDefault()
    {
        var session = NewSession();
        session.IsPromptHeld.Should().BeFalse();
    }

    [Fact]
    public void OpenPromptHold_SetsIsPromptHeld()
    {
        var session = NewSession();
        session.OpenPromptHold("swell");
        session.IsPromptHeld.Should().BeTrue();
    }

    [Fact]
    public void ReleasePromptHold_WithTwoOwners_StaysHeldUntilLastRelease()
    {
        var session = NewSession();
        session.OpenPromptHold("swell");
        session.OpenPromptHold("cutscene");

        session.ReleasePromptHold("swell");
        session.IsPromptHeld.Should().BeTrue("cutscene owner has not released yet");

        session.ReleasePromptHold("cutscene");
        session.IsPromptHeld.Should().BeFalse();
    }

    [Fact]
    public void ReleasePromptHold_ReleaseOrderDoesNotMatter()
    {
        var session = NewSession();
        session.OpenPromptHold("swell");
        session.OpenPromptHold("cutscene");

        session.ReleasePromptHold("cutscene");
        session.IsPromptHeld.Should().BeTrue("swell owner has not released yet");

        session.ReleasePromptHold("swell");
        session.IsPromptHeld.Should().BeFalse();
    }

    [Fact]
    public void ReleasePromptHold_LastOwner_ArmsNeedsPromptRefresh()
    {
        var session = NewSession();
        session.NeedsPromptRefresh = false;
        session.OpenPromptHold("swell");
        session.OpenPromptHold("cutscene");

        session.ReleasePromptHold("swell");
        session.NeedsPromptRefresh.Should().BeFalse("still held by cutscene, no redraw yet");

        session.ReleasePromptHold("cutscene");
        session.NeedsPromptRefresh.Should().BeTrue("last release must arm exactly one redraw");
    }

    [Fact]
    public void ReleasePromptHold_UnknownOwner_IsANoOp()
    {
        var session = NewSession();
        session.NeedsPromptRefresh = false;
        session.OpenPromptHold("swell");

        session.ReleasePromptHold("cutscene"); // never opened this owner

        session.IsPromptHeld.Should().BeTrue();
        session.NeedsPromptRefresh.Should().BeFalse();
    }

    [Fact]
    public void ForceReleaseAllPromptHolds_ClearsEveryOwner_AndArmsRedraw()
    {
        var session = NewSession();
        session.OpenPromptHold("swell");
        session.OpenPromptHold("cutscene");
        session.NeedsPromptRefresh = false;

        session.ForceReleaseAllPromptHolds();

        session.IsPromptHeld.Should().BeFalse();
        session.NeedsPromptRefresh.Should().BeTrue();
    }

    [Fact]
    public void ForceReleaseAllPromptHolds_NoOwners_IsANoOp()
    {
        var session = NewSession();
        session.NeedsPromptRefresh = false;

        session.ForceReleaseAllPromptHolds();

        session.NeedsPromptRefresh.Should().BeFalse();
    }
}
